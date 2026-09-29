using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Tests.Unit;

public class FeeCalculatorTests
{
    [Fact]
    public void First_child_pays_the_full_monthly_fee()
    {
        var quote = FeeCalculator.Calculate(3500m, otherActiveSiblings: 0);

        Assert.Equal(3500m, quote.Amount);
        Assert.False(quote.SiblingDiscountApplied);
        Assert.Equal(0m, quote.DiscountPercent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Second_and_later_children_get_ten_percent_off(int otherSiblings)
    {
        var quote = FeeCalculator.Calculate(3500m, otherSiblings);

        Assert.Equal(3150m, quote.Amount);
        Assert.True(quote.SiblingDiscountApplied);
        Assert.Equal(10m, quote.DiscountPercent);
    }

    [Fact]
    public void Discounted_fee_is_rounded_to_two_decimals()
    {
        // 3333.33 * 0.9 = 2999.997 -> 3000.00
        Assert.Equal(3000.00m, FeeCalculator.Calculate(3333.33m, 1).Amount);
    }

    [Fact]
    public void Free_class_stays_free()
    {
        Assert.Equal(0m, FeeCalculator.Calculate(0m, 2).Amount);
    }
}
