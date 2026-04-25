namespace Helper;

public static class GstCalculator
{
    public static (decimal taxAmount, decimal cgst, decimal sgst) SplitGst(decimal taxableAmount, decimal totalPercent)
    {
        var taxAmount = Math.Round(taxableAmount * totalPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var half = Math.Round(taxAmount / 2m, 2, MidpointRounding.AwayFromZero);
        return (taxAmount, half, taxAmount - half);
    }
}
