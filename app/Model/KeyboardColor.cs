namespace JiYaoChu.Model;

/// <summary>Strict user input validation; never substitutes a fallback color.</summary>
public static class KeyboardColor
{
    public static bool TryParse(string? input, out string color)
    {
        var digits = (input ?? "").Trim();
        if (digits.StartsWith('#')) digits = digits[1..];
        if (digits.Length != 6 || !digits.All(char.IsAsciiHexDigit))
        {
            color = "";
            return false;
        }
        color = "#" + digits.ToLowerInvariant();
        return true;
    }
}
