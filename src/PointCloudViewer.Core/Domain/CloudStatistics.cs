namespace PointCloudViewer.Core.Domain;

/// <summary>Summary statistics computed in a single pass over a point cloud.</summary>
public sealed record CloudStatistics(long PointCount, BoundingBox Bounds, Vec3 Centroid)
{
    public static CloudStatistics Compute(IEnumerable<PointRecord> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var acc = new CloudStatisticsAccumulator();
        foreach (var p in points)
        {
            acc.Add(p);
        }

        return acc.ToStatistics();
    }
}

/// <summary>
/// Streaming accumulator for <see cref="CloudStatistics"/>. Uses Kahan-compensated sums so the
/// centroid stays accurate for millions of points with large (georeferenced) coordinates.
/// </summary>
public sealed class CloudStatisticsAccumulator
{
    private long _count;
    private BoundingBox _bounds = BoundingBox.Empty;
    private KahanSum _sumX;
    private KahanSum _sumY;
    private KahanSum _sumZ;

    public long Count => _count;

    public void Add(in PointRecord point)
    {
        _count++;
        _bounds = _bounds.Include(point.X, point.Y, point.Z);
        _sumX.Add(point.X);
        _sumY.Add(point.Y);
        _sumZ.Add(point.Z);
    }

    public CloudStatistics ToStatistics()
    {
        var centroid = _count == 0
            ? Vec3.Zero
            : new Vec3(_sumX.Value / _count, _sumY.Value / _count, _sumZ.Value / _count);
        return new CloudStatistics(_count, _bounds, centroid);
    }

    private struct KahanSum
    {
        private double _sum;
        private double _compensation;

        public readonly double Value => _sum;

        public void Add(double value)
        {
            var y = value - _compensation;
            var t = _sum + y;
            _compensation = (t - _sum) - y;
            _sum = t;
        }
    }
}
