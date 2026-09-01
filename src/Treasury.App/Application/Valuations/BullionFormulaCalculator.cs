namespace Treasury.App.Application.Valuations;

public static class BullionFormulaCalculator
{
    public static decimal Calculate(decimal weight, decimal purity, decimal unitPrice)
    {
        return decimal.Round(weight * purity * unitPrice, 4, MidpointRounding.AwayFromZero);
    }
}
