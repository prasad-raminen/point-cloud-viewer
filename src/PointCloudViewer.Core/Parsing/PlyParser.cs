using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Parsing;

/// <summary>
/// Parser for ASCII PLY ("Polygon File Format") files. Only the <c>vertex</c> element is read;
/// faces and other elements are ignored, which is what a point viewer needs.
/// </summary>
/// <remarks>
/// Supported vertex properties: <c>x y z</c> (required) and optionally <c>red green blue</c>
/// (also <c>r g b</c> or <c>diffuse_red</c>...). Integer colour types are read as 0-255, float
/// colour types as 0-1. Binary PLY variants are rejected with
/// <see cref="UnsupportedPointCloudFormatException"/>.
/// </remarks>
public sealed class PlyParser : IPointCloudParser
{
    private static readonly HashSet<string> FloatTypes =
        new(StringComparer.Ordinal) { "float", "float32", "double", "float64" };

    private static readonly HashSet<string> KnownScalarTypes = new(StringComparer.Ordinal)
    {
        "char", "uchar", "short", "ushort", "int", "uint", "float", "double",
        "int8", "uint8", "int16", "uint16", "int32", "uint32", "float32", "float64",
    };

    public string Format => "ply";

    public IReadOnlyCollection<string> Extensions { get; } = [".ply"];

    public async Task<ParseResult> ParseAsync(Stream stream, ParseOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        using var reader = new StreamReader(stream, leaveOpen: true);
        var header = await ReadHeaderAsync(reader, options, cancellationToken).ConfigureAwait(false);
        var layout = VertexLayout.From(header.Vertex);

        if (header.Vertex.Count > options.MaxPoints)
        {
            throw new PointLimitExceededException(options.MaxPoints);
        }

        var diagnostics = new ParseDiagnostics(options.MaxWarnings);
        var lineNumber = header.LineCount;

        // ASCII PLY stores one line per element instance, so elements declared before
        // "vertex" can be skipped by line count.
        for (long i = 0; i < header.LinesBeforeVertex; i++)
        {
            if (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                throw new InvalidPointCloudException("PLY file ended before the vertex element.");
            }

            lineNumber++;
        }

        var points = new List<PointRecord>((int)Math.Min(header.Vertex.Count, 1_000_000));
        var columns = new Range[layout.PropertyCount];
        long consumed = 0;
        while (consumed < header.Vertex.Count)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                diagnostics.Warn($"File ended after {consumed:N0} of {header.Vertex.Count:N0} declared vertices.");
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            consumed++;
            if (layout.TryParse(line, columns, out var point, out var error))
            {
                points.Add(point);
            }
            else
            {
                diagnostics.Skip(lineNumber, error);
            }
        }

        if (points.Count == 0)
        {
            throw new InvalidPointCloudException(
                $"No valid vertices were found in the PLY file ({diagnostics.SkippedLines} malformed line(s)).");
        }

        return new ParseResult(new PointCloud(points, layout.HasColor), Format, diagnostics.SkippedLines, diagnostics.Warnings);
    }

    private static async Task<PlyHeader> ReadHeaderAsync(StreamReader reader, ParseOptions options, CancellationToken ct)
    {
        var first = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        if (first is null || !first.Trim().Equals("ply", StringComparison.Ordinal))
        {
            throw new InvalidPointCloudException("Not a PLY file: the first line must be 'ply'.");
        }

        var elements = new List<PlyElement>();
        var lineCount = 1;
        var sawFormat = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false)
                ?? throw new InvalidPointCloudException("PLY header is missing 'end_header'.");
            lineCount++;
            if (lineCount > options.MaxHeaderLines)
            {
                throw new InvalidPointCloudException($"PLY header exceeds {options.MaxHeaderLines} lines.");
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            switch (parts[0])
            {
                case "format":
                    sawFormat = true;
                    if (parts.Length < 2 || parts[1] != "ascii")
                    {
                        var variant = parts.Length > 1 ? parts[1] : "(missing)";
                        throw new UnsupportedPointCloudFormatException(
                            $"PLY encoding '{variant}' is not supported; please export the file as ASCII PLY.");
                    }

                    break;
                case "comment":
                case "obj_info":
                    break;
                case "element":
                    if (parts.Length != 3 || !long.TryParse(parts[2], out var count) || count < 0)
                    {
                        throw new InvalidPointCloudException($"Invalid PLY element declaration: '{line.Trim()}'.");
                    }

                    elements.Add(new PlyElement(parts[1], count));
                    break;
                case "property":
                    if (elements.Count == 0)
                    {
                        throw new InvalidPointCloudException("PLY property declared before any element.");
                    }

                    elements[^1].Properties.Add(ParseProperty(parts, line));
                    break;
                case "end_header":
                    if (!sawFormat)
                    {
                        throw new InvalidPointCloudException("PLY header has no 'format' line.");
                    }

                    var vertexIndex = elements.FindIndex(e => e.Name == "vertex");
                    if (vertexIndex < 0)
                    {
                        throw new InvalidPointCloudException("PLY header does not declare a 'vertex' element.");
                    }

                    var linesBefore = elements.Take(vertexIndex).Sum(e => e.Count);
                    return new PlyHeader(elements[vertexIndex], linesBefore, lineCount);
                default:
                    throw new InvalidPointCloudException($"Unexpected PLY header line: '{line.Trim()}'.");
            }
        }
    }

    private static PlyProperty ParseProperty(string[] parts, string line)
    {
        if (parts.Length >= 2 && parts[1] == "list")
        {
            return parts.Length == 5
                ? new PlyProperty(parts[4], "list", IsList: true)
                : throw new InvalidPointCloudException($"Invalid PLY list property: '{line.Trim()}'.");
        }

        if (parts.Length != 3 || !KnownScalarTypes.Contains(parts[1]))
        {
            throw new InvalidPointCloudException($"Invalid PLY property declaration: '{line.Trim()}'.");
        }

        return new PlyProperty(parts[2], parts[1], IsList: false);
    }

    private sealed record PlyHeader(PlyElement Vertex, long LinesBeforeVertex, long LineCount);

    private sealed record PlyElement(string Name, long Count)
    {
        public List<PlyProperty> Properties { get; } = [];
    }

    private sealed record PlyProperty(string Name, string Type, bool IsList);

    /// <summary>Column indices of the vertex properties this viewer understands.</summary>
    private sealed class VertexLayout
    {
        private int _x = -1;
        private int _y = -1;
        private int _z = -1;
        private int _r = -1;
        private int _g = -1;
        private int _b = -1;
        private bool _colorIsNormalized;

        public int PropertyCount { get; private init; }

        public bool HasColor => _r >= 0 && _g >= 0 && _b >= 0;

        private int RequiredColumns { get; set; }

        public static VertexLayout From(PlyElement vertex)
        {
            if (vertex.Properties.Any(p => p.IsList))
            {
                throw new UnsupportedPointCloudFormatException("PLY vertex elements with list properties are not supported.");
            }

            var layout = new VertexLayout { PropertyCount = vertex.Properties.Count };
            for (var i = 0; i < vertex.Properties.Count; i++)
            {
                var p = vertex.Properties[i];
                switch (p.Name)
                {
                    case "x": layout._x = i; break;
                    case "y": layout._y = i; break;
                    case "z": layout._z = i; break;
                    case "red" or "r" or "diffuse_red":
                        layout._r = i;
                        layout._colorIsNormalized = FloatTypes.Contains(p.Type);
                        break;
                    case "green" or "g" or "diffuse_green": layout._g = i; break;
                    case "blue" or "b" or "diffuse_blue": layout._b = i; break;
                }
            }

            if (layout._x < 0 || layout._y < 0 || layout._z < 0)
            {
                throw new InvalidPointCloudException("PLY vertex element must declare x, y and z properties.");
            }

            var used = new[] { layout._x, layout._y, layout._z }.ToList();
            if (layout.HasColor)
            {
                used.AddRange([layout._r, layout._g, layout._b]);
            }

            layout.RequiredColumns = used.Max() + 1;
            return layout;
        }

        public bool TryParse(string line, Span<Range> columns, out PointRecord point, out string error)
        {
            point = default;
            error = string.Empty;
            var span = line.AsSpan();
            var count = LineTokenizer.Tokenize(span, columns);
            if (count < RequiredColumns)
            {
                error = $"expected {PropertyCount} values but found {count}";
                return false;
            }

            if (!LineTokenizer.TryParseFinite(span[columns[_x]], out var x) ||
                !LineTokenizer.TryParseFinite(span[columns[_y]], out var y) ||
                !LineTokenizer.TryParseFinite(span[columns[_z]], out var z))
            {
                error = "coordinates are not valid finite numbers";
                return false;
            }

            if (!HasColor)
            {
                point = PointRecord.Uncolored(x, y, z);
                return true;
            }

            if (!LineTokenizer.TryParseFinite(span[columns[_r]], out var r) ||
                !LineTokenizer.TryParseFinite(span[columns[_g]], out var g) ||
                !LineTokenizer.TryParseFinite(span[columns[_b]], out var b))
            {
                error = "colour values are not valid numbers";
                return false;
            }

            point = new PointRecord(
                x, y, z,
                LineTokenizer.ToChannel(r, _colorIsNormalized),
                LineTokenizer.ToChannel(g, _colorIsNormalized),
                LineTokenizer.ToChannel(b, _colorIsNormalized));
            return true;
        }
    }
}
