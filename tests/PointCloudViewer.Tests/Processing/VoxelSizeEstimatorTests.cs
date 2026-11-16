using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Processing;

namespace PointCloudViewer.Tests.Processing;

public class VoxelSizeEstimatorTests
{
    [Fact]
    public void Returns_null_when_cloud_is_already_under_target()
    {
        var points = Enumerable.Range(0, 10).Select(i => PointRecord.Uncolored(i, 0, 0)).ToList();

        Assert.Null(VoxelSizeEstimator.EstimateForTarget(points, targetPoints: 100));
    }

    [Fact]
    public void Returns_null_for_zero_extent_cloud()
    {
        var points = Enumerable.Repeat(PointRecord.Uncolored(1, 1, 1), 50).ToList();

        Assert.Null(VoxelSizeEstimator.EstimateForTarget(points, targetPoints: 10));
    }

    [Fact]
    public void Estimated_size_meets_target_without_overshooting_much_for_a_flat_terrain()
    {
        // A flat 300 x 300 grid: volume-based estimates fail here, measurement-based search must not.
        var points = new List<PointRecord>();
        for (var x = 0; x < 300; x++)
        {
            for (var y = 0; y < 300; y++)
            {
                points.Add(PointRecord.Uncolored(x * 0.1, y * 0.1, Math.Sin(x * 0.05) * 0.2));
            }
        }

        const int target = 10_000;
        var voxel = VoxelSizeEstimator.EstimateForTarget(points, target);

        Assert.NotNull(voxel);
        var result = new VoxelGridDownsampler().Downsample(new PointCloud(points, false), voxel.Value);
        Assert.InRange(result.Count, target / 2, target);
    }
}
