using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Agents.Tools;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Agents;

/// <summary>
/// Agent 4 (deterministic, NO LLM): the safety net. It re-checks every other
/// agent's output against hard business rules using fresh database data, and
/// scans the parent's notes for prompt-injection attempts.
/// It loads the facts, then hands them to the pure functions in ValidationRules.
/// </summary>
public class ValidationSafetyAgent(AppDbContext db, IFeeService fees)
{
    public const string Name = "ValidationSafetyAgent";
    public static readonly IReadOnlySet<string> AllowedTools = new HashSet<string>(); // reads the DB directly, read-only

    public async Task<ValidationOutput> RunAsync(ValidationInput input, ToolGateway tools, CancellationToken ct)
    {
        var results = new List<RuleResult>
        {
            ValidationRules.PlanSchema(input.Plan),
            ValidationRules.SkillSchema(input.Skill),
            ValidationRules.PlacementSchema(input.Placement),
            ValidationRules.CandidateMembership(input.Placement.ClassId, input.Candidates.Candidates.Select(c => c.Id))
        };

        var klass = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.Placement.ClassId, ct);
        if (klass is null)
        {
            results.Add(new RuleResult("ClassExists", false, ValidationSeverity.Error, $"Class {input.Placement.ClassId} does not exist."));
        }
        else
        {
            results.Add(ValidationRules.ClassActive(klass.IsActive));

            var seatsTaken = await db.Enrolments.CountAsync(e => e.AssignedClassId == klass.Id && e.Status == EnrolmentStatus.Approved, ct);
            results.Add(ValidationRules.Capacity(klass.Capacity, seatsTaken));

            // Other classes this child already has an approved place in.
            var childsClasses = await db.Enrolments.AsNoTracking()
                .Where(e => e.ChildId == input.ChildId && e.Status == EnrolmentStatus.Approved && e.Id != input.EnrolmentId)
                .Select(e => e.AssignedClass!)
                .ToListAsync(ct);
            results.Add(ValidationRules.TimeClash(klass, childsClasses));

            results.Add(ValidationRules.LevelFit(input.Skill.Level, klass.Level, input.Placement.LevelToleranceReason));

            var recomputed = await fees.QuoteAsync(input.ChildId, klass.Id, ct);
            results.Add(ValidationRules.FeeMatch(input.Placement.Fee.Amount, recomputed.Amount));
        }

        results.Add(ValidationRules.PromptInjection(input.ParentNotes));

        var hardRulesPassed = results.Where(r => r.Severity == ValidationSeverity.Error).All(r => r.Passed);
        var injection = results.Any(r => r.RuleName == ValidationRules.PromptInjectionRule && !r.Passed);
        return new ValidationOutput(hardRulesPassed, injection, results);
    }
}

/// <summary>Pure rule functions: no database, no LLM, so each one is easy to unit test.</summary>
public static class ValidationRules
{
    public const string PromptInjectionRule = "PromptInjection";

    public static RuleResult PlanSchema(PlannerOutput plan)
    {
        try
        {
            PlanValidator.EnsureValid(plan);
            return Pass("PlanSchema", "The plan uses only allowed steps, in order.");
        }
        catch (AgentOutputException ex)
        {
            return Fail("PlanSchema", ex.Message);
        }
    }

    public static RuleResult SkillSchema(SkillOutput skill) =>
        Enum.IsDefined(skill.Level) && skill.Confidence is "Low" or "Medium" or "High" && !string.IsNullOrWhiteSpace(skill.Rationale)
            ? Pass("SkillSchema", $"Skill output is well-formed ({skill.Level}, {skill.Confidence} confidence).")
            : Fail("SkillSchema", "Skill output is missing a level, confidence or rationale.");

    public static RuleResult PlacementSchema(PlacementOutput placement) =>
        placement.ClassId > 0 && !string.IsNullOrWhiteSpace(placement.Reason) && placement.Fee.Amount >= 0
            ? Pass("PlacementSchema", "Placement output is well-formed.")
            : Fail("PlacementSchema", "Placement output is missing a class, a reason or a valid fee.");

    public static RuleResult CandidateMembership(int classId, IEnumerable<int> candidateIds) =>
        candidateIds.Contains(classId)
            ? Pass("CandidateMembership", $"Class {classId} was one of the searched candidates.")
            : Fail("CandidateMembership", $"Class {classId} was not returned by the class search.");

    public static RuleResult ClassActive(bool isActive) =>
        isActive ? Pass("ClassActive", "The class is active.") : Fail("ClassActive", "The class is no longer active.");

    public static RuleResult Capacity(int capacity, int seatsTaken) =>
        seatsTaken < capacity
            ? Pass("Capacity", $"{capacity - seatsTaken} of {capacity} seats free.")
            : Fail("Capacity", $"The class is full ({seatsTaken}/{capacity}).");

    public static RuleResult TimeClash(ChessClass proposed, IEnumerable<ChessClass> childsOtherClasses)
    {
        var clash = childsOtherClasses.FirstOrDefault(c => proposed.OverlapsWith(c.DayOfWeek, c.StartTime, c.EndTime));
        return clash is null
            ? Pass("TimeClash", "No overlap with the child's other classes.")
            : Fail("TimeClash", $"Overlaps with '{clash.Name}' ({clash.DayOfWeek} {clash.StartTime:HH\\:mm}-{clash.EndTime:HH\\:mm}).");
    }

    /// <summary>Exact level passes. One level away passes only with a written reason. Two levels never.</summary>
    public static RuleResult LevelFit(ClassLevel assessed, ClassLevel classLevel, string? toleranceReason)
    {
        var distance = LevelRules.Distance(assessed, classLevel);
        if (distance == 0) return Pass("LevelFit", $"Class level {classLevel} matches the assessed level.");
        if (distance == 1 && !string.IsNullOrWhiteSpace(toleranceReason))
            return Pass("LevelFit", $"Class level {classLevel} is one level from {assessed}; reason recorded: {toleranceReason}");
        return Fail("LevelFit", distance == 1
            ? $"Class level {classLevel} differs from {assessed} and no reason was recorded."
            : $"Class level {classLevel} is too far from the assessed level {assessed}.");
    }

    public static RuleResult FeeMatch(decimal proposed, decimal recomputed) =>
        proposed == recomputed
            ? Pass("FeeMatch", $"Fee {recomputed:0.00} matches the fee rule.")
            : Fail("FeeMatch", $"Proposed fee {proposed:0.00} does not match the fee rule ({recomputed:0.00}).");

    /// <summary>Warning, not error: the Admin sees the flag, and nothing is ever auto-approved anyway.</summary>
    public static RuleResult PromptInjection(string? notes)
    {
        var hits = PromptInjectionDetector.Find(notes);
        return hits.Count == 0
            ? new RuleResult(PromptInjectionRule, true, ValidationSeverity.Warning, "No instruction-like text in the parent's notes.")
            : new RuleResult(PromptInjectionRule, false, ValidationSeverity.Warning,
                $"Parent notes contain instruction-like text ({string.Join("; ", hits)}). Review manually.");
    }

    private static RuleResult Pass(string rule, string message) => new(rule, true, ValidationSeverity.Error, message);
    private static RuleResult Fail(string rule, string message) => new(rule, false, ValidationSeverity.Error, message);
}

/// <summary>Looks for text that tries to give orders to an AI or the system.</summary>
public static class PromptInjectionDetector
{
    private static readonly (string Label, Regex Pattern)[] Patterns =
    {
        ("ignore instructions", Rx(@"\b(ignore|disregard|forget|override)\b.{0,30}\b(previous|prior|above|earlier|all|your|the)\b.{0,20}\b(instructions?|rules?|prompts?)\b")),
        ("auto-approve request", Rx(@"\b(auto[\s-]?approve|approve\s+(this|it|me|automatically|immediately|now)|mark\s+(as\s+)?approved)\b")),
        ("role change", Rx(@"\b(you\s+are\s+now|act\s+as|pretend\s+to\s+be)\b")),
        ("system prompt", Rx(@"\bsystem\s+prompt\b|<\s*/?\s*(system|data|instructions?)\s*>")),
        ("change fee or status", Rx(@"\b(set|change|make)\b.{0,20}\b(fee|price|status|capacity)\b")),
        ("skip checks", Rx(@"\b(skip|bypass|disable)\b.{0,20}\b(validation|checks?|rules?|approval)\b"))
    };

    private static Regex Rx(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    /// <summary>Returns a label for each kind of suspicious text found (empty list = clean).</summary>
    public static IReadOnlyList<string> Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return Patterns.Where(p => p.Pattern.IsMatch(text)).Select(p => p.Label).ToList();
    }
}
