namespace PointCloudViewer.Core.Domain;

/// <summary>An in-memory point cloud.</summary>
public sealed class PointCloud
{
    public PointCloud(IReadOnlyList<PointRecord> points, bool hasColor)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = points;
        HasColor = hasColor;
    }

    public IReadOnlyList<PointRecord> Points { get; }

    /// <summary>True when the source data carried per-point colour.</summary>
    public bool HasColor { get; }

    public int Count => Points.Count;
}
