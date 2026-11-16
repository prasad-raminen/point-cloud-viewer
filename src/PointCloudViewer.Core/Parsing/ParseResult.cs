using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Parsing;

/// <summary>Outcome of parsing a file: the cloud plus diagnostics about lines that were skipped.</summary>
public sealed record ParseResult(PointCloud Cloud, string Format, int SkippedLines, IReadOnlyList<string> Warnings);

/// <summary>Collects bounded diagnostics while parsing.</summary>
internal sealed class ParseDiagnostics(int maxWarnings)
{
    private readonly List<string> _warnings = [];

    public int SkippedLines { get; private set; }

    public IReadOnlyList<string> Warnings => _warnings;

    public void Skip(long lineNumber, string reason)
    {
        SkippedLines++;
        Warn($"Line {lineNumber}: {reason}");
    }

    public void Warn(string message)
    {
        if (_warnings.Count < maxWarnings)
        {
            _warnings.Add(message);
        }
    }
}
