namespace PointCloudViewer.Core.Parsing;

/// <summary>Parses one point cloud file format from a stream.</summary>
public interface IPointCloudParser
{
    /// <summary>Short, lower-case format identifier (e.g. "xyz").</summary>
    string Format { get; }

    /// <summary>File extensions handled by this parser, including the leading dot.</summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>
    /// Reads the stream line by line. Malformed lines are skipped and reported in
    /// <see cref="ParseResult.Warnings"/>; structural problems throw <see cref="PointCloudException"/>.
    /// </summary>
    Task<ParseResult> ParseAsync(Stream stream, ParseOptions options, CancellationToken cancellationToken = default);
}
