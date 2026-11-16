using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Processing;

/// <summary>
/// Voxel-grid downsampling: space is divided into cubic cells of edge <c>voxelSize</c> anchored at
/// the cloud's minimum corner, and all points falling into a cell are replaced by a single point
/// at their centroid with their averaged colour.
/// </summary>
/// <remarks>
/// This is the same strategy used by common point cloud libraries for level-of-detail generation:
/// it preserves the overall shape and spatial distribution far better than random sampling
/// because dense areas are thinned while sparse areas are kept.
/// Output order is deterministic (order in which each voxel was first hit).
/// </remarks>
public sealed class VoxelGridDownsampler : IPointCloudDownsampler
{
    public PointCloud Downsample(PointCloud cloud, double voxelSize)
    {
        ArgumentNullException.ThrowIfNull(cloud);
        ValidateVoxelSize(voxelSize);

        if (cloud.Count == 0)
        {
            return cloud;
        }

        var origin = BoundingBox.FromPoints(cloud.Points).Min;
        var index = new Dictionary<VoxelKey, int>();
        var cells = new List<VoxelAccumulator>();

        foreach (var p in cloud.Points)
        {
            var key = VoxelKey.For(p, origin, voxelSize);
            if (!index.TryGetValue(key, out var slot))
            {
                slot = cells.Count;
                index.Add(key, slot);
                cells.Add(default);
            }

            var cell = cells[slot];
            cell.Add(p);
            cells[slot] = cell;
        }

        var result = new PointRecord[cells.Count];
        for (var i = 0; i < cells.Count; i++)
        {
            result[i] = cells[i].ToPoint();
        }

        return new PointCloud(result, cloud.HasColor);
    }

    /// <summary>Counts how many occupied voxels a given voxel size would produce.</summary>
    public static int CountOccupiedVoxels(IReadOnlyList<PointRecord> points, Vec3 origin, double voxelSize)
    {
        ArgumentNullException.ThrowIfNull(points);
        ValidateVoxelSize(voxelSize);
        var keys = new HashSet<VoxelKey>();
        foreach (var p in points)
        {
            keys.Add(VoxelKey.For(p, origin, voxelSize));
        }

        return keys.Count;
    }

    private static void ValidateVoxelSize(double voxelSize)
    {
        if (!double.IsFinite(voxelSize) || voxelSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelSize), voxelSize, "Voxel size must be a positive, finite number.");
        }
    }

    private readonly record struct VoxelKey(long X, long Y, long Z)
    {
        public static VoxelKey For(in PointRecord p, Vec3 origin, double voxelSize) => new(
            (long)Math.Floor((p.X - origin.X) / voxelSize),
            (long)Math.Floor((p.Y - origin.Y) / voxelSize),
            (long)Math.Floor((p.Z - origin.Z) / voxelSize));
    }

    private struct VoxelAccumulator
    {
        private double _x;
        private double _y;
        private double _z;
        private long _r;
        private long _g;
        private long _b;
        private int _count;

        public void Add(in PointRecord p)
        {
            _x += p.X;
            _y += p.Y;
            _z += p.Z;
            _r += p.R;
            _g += p.G;
            _b += p.B;
            _count++;
        }

        public readonly PointRecord ToPoint() => new(
            _x / _count,
            _y / _count,
            _z / _count,
            (byte)Math.Round((double)_r / _count, MidpointRounding.AwayFromZero),
            (byte)Math.Round((double)_g / _count, MidpointRounding.AwayFromZero),
            (byte)Math.Round((double)_b / _count, MidpointRounding.AwayFromZero));
    }
}
