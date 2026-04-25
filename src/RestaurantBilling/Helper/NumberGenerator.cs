namespace Helper;

public static class NumberGenerator
{
    public static string Build(string prefix, int runningNo, int numberLength, DateOnly businessDate)
        => $"{prefix}-{businessDate:yyyy}-{runningNo.ToString().PadLeft(numberLength, '0')}";
}
