using System.Globalization;

namespace PointCloudViewer.Core.Parsing;

/// <summary>Allocation-free helpers for splitting and reading numeric text tokens.</summary>
internal static class LineTokenizer
{
    /// <summary>
    /// Splits <paramref name="line"/> on whitespace, commas and semicolons, writing up to
    /// <c>tokens.Length</c> ranges. Returns the total number of tokens in the line (which may
    /// exceed the capacity of <paramref name="tokens"/>).
    /// </summary>
    public static int Tokenize(ReadOnlySpan<char> line, Span<Range> tokens)
    {
        var count = 0;
        var i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && IsSeparator(line[i]))
            {
                i++;
            }

            if (i >= line.Length)
            {
                break;
            }

            var start = i;
            while (i < line.Length && !IsSeparator(line[i]))
            {
                i++;
            }

            if (count < tokens.Length)
            {
                tokens[count] = start..i;
            }

            count++;
        }

        return count;
    }

    public static bool TryParseFinite(ReadOnlySpan<char> token, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>Converts a colour channel to a byte, clamping out-of-range values.</summary>
    public static byte ToChannel(double value, bool normalized)
    {
        var scaled = normalized ? value * 255.0 : value;
        return (byte)Math.Clamp(Math.Round(scaled, MidpointRounding.AwayFromZero), 0, 255);
    }

    public static bool IsCommentOrBlank(ReadOnlySpan<char> line)
    {
        var trimmed = line.TrimStart();
        return trimmed.IsEmpty || trimmed[0] == '#' || trimmed.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool IsSeparator(char c) => char.IsWhiteSpace(c) || c == ',' || c == ';';
}
