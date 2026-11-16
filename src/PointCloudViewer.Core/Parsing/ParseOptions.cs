namespace PointCloudViewer.Core.Parsing;

/// <summary>Limits applied while parsing untrusted input.</summary>
public sealed record ParseOptions
{
    public static ParseOptions Default { get; } = new();

    /// <summary>Maximum number of points accepted before parsing is aborted.</summary>
    public int MaxPoints { get; init; } = 5_000_000;

    /// <summary>Maximum number of per-line warnings retained (the skip counter is unbounded).</summary>
    public int MaxWarnings { get; init; } = 20;

    /// <summary>Maximum number of PLY header lines read before the header is considered invalid.</summary>
    public int MaxHeaderLines { get; init; } = 512;
}
