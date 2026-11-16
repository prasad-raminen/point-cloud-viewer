using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Parsing;

/// <summary>
/// Parser for ASCII XYZ files: one point per line as <c>x y z [r g b]</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Values may be separated by spaces, tabs, commas or semicolons.</item>
/// <item>Lines starting with <c>#</c> or <c>//</c> and blank lines are ignored.</item>
/// <item>Columns 4-6, when present, are read as 0-255 RGB; with 4 or 5 columns the extra values
/// (typically intensity) are ignored.</item>
/// <item>Lines with fewer than three numeric values or non-finite numbers are skipped and reported.</item>
/// </list>
/// </remarks>
public sealed class XyzParser : IPointCloudParser
{
    private const int MaxColumns = 6;

    public string Format => "xyz";

    public IReadOnlyCollection<string> Extensions { get; } = [".xyz", ".txt", ".pts", ".csv"];

    public async Task<ParseResult> ParseAsync(Stream stream, ParseOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        var points = new List<PointRecord>();
        var diagnostics = new ParseDiagnostics(options.MaxWarnings);
        var hasColor = false;
        long lineNumber = 0;

        using var reader = new StreamReader(stream, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (LineTokenizer.IsCommentOrBlank(line))
            {
                continue;
            }

            if (!TryParseLine(line, out var point, out var lineHasColor, out var error))
            {
                diagnostics.Skip(lineNumber, error);
                continue;
            }

            if (points.Count >= options.MaxPoints)
            {
                throw new PointLimitExceededException(options.MaxPoints);
            }

            hasColor |= lineHasColor;
            points.Add(point);
        }

        if (points.Count == 0)
        {
            throw new InvalidPointCloudException(
                $"No valid points were found in the XYZ file ({diagnostics.SkippedLines} malformed line(s)).");
        }

        return new ParseResult(new PointCloud(points, hasColor), Format, diagnostics.SkippedLines, diagnostics.Warnings);
    }

    private static bool TryParseLine(string line, out PointRecord point, out bool hasColor, out string error)
    {
        point = default;
        hasColor = false;
        error = string.Empty;

        Span<Range> tokens = stackalloc Range[MaxColumns];
        var span = line.AsSpan();
        var count = LineTokenizer.Tokenize(span, tokens);
        if (count < 3)
        {
            error = $"expected at least 3 values but found {count}";
            return false;
        }

        if (!LineTokenizer.TryParseFinite(span[tokens[0]], out var x) ||
            !LineTokenizer.TryParseFinite(span[tokens[1]], out var y) ||
            !LineTokenizer.TryParseFinite(span[tokens[2]], out var z))
        {
            error = "coordinates are not valid finite numbers";
            return false;
        }

        if (count < MaxColumns)
        {
            point = PointRecord.Uncolored(x, y, z);
            return true;
        }

        if (!LineTokenizer.TryParseFinite(span[tokens[3]], out var r) ||
            !LineTokenizer.TryParseFinite(span[tokens[4]], out var g) ||
            !LineTokenizer.TryParseFinite(span[tokens[5]], out var b))
        {
            error = "colour values are not valid numbers";
            return false;
        }

        hasColor = true;
        point = new PointRecord(
            x, y, z,
            LineTokenizer.ToChannel(r, normalized: false),
            LineTokenizer.ToChannel(g, normalized: false),
            LineTokenizer.ToChannel(b, normalized: false));
        return true;
    }
}
