namespace Helper;

public static class AmountToWords
{
    private static readonly string[] Ones = ["", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string Inr(decimal amount)
    {
        var rupees = (long)Math.Floor(amount);
        var paise = (int)Math.Round((amount - rupees) * 100m, MidpointRounding.AwayFromZero);
        return $"{ToWords(rupees)} Rupees {(paise > 0 ? $"and {ToWords(paise)} Paise " : string.Empty)}Only";
    }

    private static string ToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 20) return Ones[number];
        if (number < 100) return $"{Tens[number / 10]} {ToWords(number % 10)}".Trim();
        if (number < 1000) return $"{Ones[number / 100]} Hundred {ToWords(number % 100)}".Trim();
        if (number < 100000) return $"{ToWords(number / 1000)} Thousand {ToWords(number % 1000)}".Trim();
        if (number < 10000000) return $"{ToWords(number / 100000)} Lakh {ToWords(number % 100000)}".Trim();
        return $"{ToWords(number / 10000000)} Crore {ToWords(number % 10000000)}".Trim();
    }
}
