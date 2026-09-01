using FluentAssertions;
using Treasury.App.Application.Valuations;

namespace Treasury.Domain.Tests;

public class BullionFormulaCalculatorTests
{
    [Fact]
    public void Calculate_Uses_Weight_Purity_And_UnitPrice()
    {
        var value = BullionFormulaCalculator.Calculate(100m, 0.9999m, 320m);

        value.Should().Be(31996.8m);
    }

    [Fact]
    public void Calculate_Rounds_Away_From_Zero_To_4_Decimals()
    {
        var value = BullionFormulaCalculator.Calculate(1m, 1m, 1.23456m);

        value.Should().Be(1.2346m);
    }
}
