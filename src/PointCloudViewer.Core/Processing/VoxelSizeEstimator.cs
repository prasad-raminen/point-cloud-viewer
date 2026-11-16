using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Processing;

/// <summary>
/// Finds a voxel size whose downsampled output has at most a target number of points.
/// </summary>
/// <remarks>
/// The number of occupied voxels decreases (almost) monotonically as the voxel size grows, so a
/// bisection in log space converges in a handful of passes over the data. A volume-based guess would be badly wrong for flat scenes such as terrain, which is why
/// the occupied count is measured instead of assumed.
/// </remarks>
public static class VoxelSizeEstimator
{
    /// <returns>
    /// A voxel size whose occupied-cell count is at most <paramref name="targetPoints"/>, or
    /// <c>null</c> when the cloud is already small enough (or has zero extent) and needs no reduction.
    /// </returns>
    public static double? EstimateForTarget(IReadOnlyList<PointRecord> points, int targetPoints, int iterations = 14)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetPoints, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        var bounds = BoundingBox.FromPoints(points);
        var diagonal = bounds.Diagonal;
        if (points.Count <= targetPoints || diagonal <= 0)
        {
            return null;
        }

        // hi: a voxel this large holds everything in at most 8 cells; lo: effectively "no reduction".
        var hi = diagonal;
        var lo = diagonal * 1e-7;
        for (var i = 0; i < iterations; i++)
        {
            var mid = Math.Sqrt(lo * hi);
            var occupied = VoxelGridDownsampler.CountOccupiedVoxels(points, bounds.Min, mid);
            if (occupied > targetPoints)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return hi;
    }
}
