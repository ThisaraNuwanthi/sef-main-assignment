using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Agents;

// The typed contracts between the orchestrator and each agent.
// Every agent takes exactly one input record and returns one output record,
// and those records are what gets stored in AgentStep.InputJson / OutputJson.

/// <summary>The only step names a plan may contain.</summary>
public static class PlanSteps
{
    public const string AssessSkill = "AssessSkill";
    public const string FindCandidateClasses = "FindCandidateClasses";
    public const string ProposePlacement = "ProposePlacement";
    public const string Validate = "Validate";
    public const string RequestApproval = "RequestApproval";

    /// <summary>The allowed steps, in the order they must run.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        AssessSkill, FindCandidateClasses, ProposePlacement, Validate, RequestApproval
    };
}

// ---------- 1. PlannerAgent ----------
public record PlannerInput(string Objective);
public record PlannerOutput(List<string> Steps, string? Summary);

// ---------- 2. SkillAssessmentAgent ----------
public record SkillInput(string ChildName, int Age, string? LichessUsername);

/// <param name="Confidence">Low | Medium | High</param>
/// <param name="Source">"Lichess" when based on real ratings, "AgeDefault" when not.</param>
public record SkillOutput(ClassLevel Level, string Confidence, string Rationale, string Source, int? Rating);

// ---------- 3. PlacementAgent (two plan steps) ----------
/// <param name="ChildId">Used to leave out classes that clash with the child's existing timetable.</param>
public record CandidateSearchInput(int ChildId, ClassLevel AssessedLevel, List<DayOfWeek> PreferredDays, TimeOnly? From, TimeOnly? To);

public record CandidateClass(
    int Id, string Name, ClassLevel Level, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime,
    int SeatsLeft, decimal MonthlyFee, int LevelDistance);

public record CandidateSearchOutput(List<CandidateClass> Candidates);

public record ProposalInput(
    int ChildId, ClassLevel AssessedLevel, List<CandidateClass> Candidates, int? RequestedClassId, string? ParentNotes);

public record PlacementOutput(int ClassId, string ClassName, string Reason, string? LevelToleranceReason, FeeQuote Fee);

// ---------- 4. ValidationSafetyAgent ----------
public record ValidationInput(
    int EnrolmentId, int ChildId, PlannerOutput Plan, SkillOutput Skill,
    CandidateSearchOutput Candidates, PlacementOutput Placement, string? ParentNotes);

public record RuleResult(string RuleName, bool Passed, ValidationSeverity Severity, string Message);

public record ValidationOutput(bool AllHardRulesPassed, bool InjectionSuspected, List<RuleResult> Results);

// ---------- Final proposal the Admin reviews (stored in AgentWorkflow.FinalOutcome) ----------
public record PlacementProposal(
    int ClassId,
    string ClassName,
    ClassLevel AssessedLevel,
    string SkillConfidence,
    string SkillRationale,
    string PlacementReason,
    string? LevelToleranceReason,
    decimal FeeAmount,
    bool SiblingDiscountApplied,
    bool InjectionSuspected);
