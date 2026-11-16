namespace PointCloudViewer.Core.Parsing;

/// <summary>Selects a parser for an uploaded file based on its extension.</summary>
public interface IPointCloudParserResolver
{
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <exception cref="UnsupportedPointCloudFormatException">No parser handles the extension.</exception>
    IPointCloudParser Resolve(string fileName);
}

public sealed class PointCloudParserResolver : IPointCloudParserResolver
{
    private readonly Dictionary<string, IPointCloudParser> _byExtension;

    public PointCloudParserResolver(IEnumerable<IPointCloudParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);
        _byExtension = new Dictionary<string, IPointCloudParser>(StringComparer.OrdinalIgnoreCase);
        foreach (var parser in parsers)
        {
            foreach (var ext in parser.Extensions)
            {
                _byExtension[ext] = parser;
            }
        }

        SupportedExtensions = _byExtension.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyCollection<string> SupportedExtensions { get; }

    public IPointCloudParser Resolve(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var ext = Path.GetExtension(fileName);
        return _byExtension.TryGetValue(ext, out var parser)
            ? parser
            : throw new UnsupportedPointCloudFormatException(
                $"Unsupported file type '{(string.IsNullOrEmpty(ext) ? "(none)" : ext)}'. " +
                $"Supported: {string.Join(", ", SupportedExtensions)}.");
    }
}
