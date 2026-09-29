using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Services;

public record FeeQuote(decimal BaseFee, bool SiblingDiscountApplied, decimal DiscountPercent, decimal Amount);

/// <summary>
/// THE fee rule, in one place. The placement agent's tool, the validation agent
/// and the approval transaction all call this, so they can never disagree.
/// </summary>
public static class FeeCalculator
{
    public const decimal SiblingDiscountPercent = 10m;

    /// <param name="monthlyFee">The class's monthly fee.</param>
    /// <param name="otherActiveSiblings">Brothers/sisters of this child who already have an approved place.</param>
    public static FeeQuote Calculate(decimal monthlyFee, int otherActiveSiblings)
    {
        // The 2nd, 3rd... active child of the same parent gets the discount; the first pays full price.
        var discounted = otherActiveSiblings > 0;
        var percent = discounted ? SiblingDiscountPercent : 0m;
        var amount = Math.Round(monthlyFee * (100m - percent) / 100m, 2, MidpointRounding.AwayFromZero);
        return new FeeQuote(monthlyFee, discounted, percent, amount);
    }
}

public interface IFeeService
{
    Task<FeeQuote> QuoteAsync(int childId, int classId, CancellationToken ct = default);
}

/// <summary>Loads the facts the fee rule needs from the database, then applies FeeCalculator.</summary>
public class FeeService(AppDbContext db) : IFeeService
{
    public async Task<FeeQuote> QuoteAsync(int childId, int classId, CancellationToken ct = default)
    {
        var monthlyFee = await db.Classes.Where(c => c.Id == classId).Select(c => (decimal?)c.MonthlyFee).FirstOrDefaultAsync(ct)
                         ?? throw new NotFoundException($"Class {classId} was not found.");
        var parentId = await db.Children.Where(c => c.Id == childId).Select(c => (int?)c.ParentId).FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException($"Child {childId} was not found.");

        // "Active" sibling = another child of the same parent with at least one approved place.
        var otherActiveSiblings = await db.Enrolments
            .Where(e => e.Status == EnrolmentStatus.Approved && e.ChildId != childId && e.Child!.ParentId == parentId)
            .Select(e => e.ChildId)
            .Distinct()
            .CountAsync(ct);

        return FeeCalculator.Calculate(monthlyFee, otherActiveSiblings);
    }
}
