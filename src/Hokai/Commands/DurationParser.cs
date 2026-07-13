namespace Hokai.Commands;

public static class DurationParser
{
    private static readonly Dictionary<string, TimeSpan> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ms"] = TimeSpan.FromMilliseconds(1),
        ["s"] = TimeSpan.FromSeconds(1),
        ["m"] = TimeSpan.FromMinutes(1),
        ["h"] = TimeSpan.FromHours(1),
        ["d"] = TimeSpan.FromDays(1),
    };

    public static bool TryParse(string input, out TimeSpan result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Try standard TimeSpan parsing first
        if (TimeSpan.TryParse(input, out result))
            return result > TimeSpan.Zero;

        // Match: number + optional unit (e.g., "30s", "5m", "2h", "1d", "500ms")
        var span = input.AsSpan().Trim();

        var i = 0;
        while (i < span.Length && (char.IsDigit(span[i]) || span[i] == '.'))
            i++;

        if (i == 0) return false;

        if (!long.TryParse(span[..i], out var value))
            return false;

        var unit = i < span.Length ? span[i..].Trim().ToString() : "s";

        if (!Units.TryGetValue(unit, out var multiplier))
            return false;

        try
        {
            result = multiplier.Multiply(value);
            return result > TimeSpan.Zero;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
