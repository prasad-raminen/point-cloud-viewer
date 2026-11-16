using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Processing;

namespace PointCloudViewer.Tests.Processing;

public class VoxelGridDownsamplerTests
{
    private readonly VoxelGridDownsampler _downsampler = new();

    [Fact]
    public void Points_in_the_same_voxel_collapse_to_their_centroid_with_averaged_colour()
    {
        var cloud = new PointCloud(
            [
                new PointRecord(0.0, 0.0, 0.0, 0, 0, 0),
                new PointRecord(0.4, 0.2, 0.0, 100, 50, 255),
                new PointRecord(0.2, 0.4, 0.6, 200, 100, 0),
            ],
            hasColor: true);

        var result = _downsampler.Downsample(cloud, voxelSize: 1.0);

        var p = Assert.Single(result.Points);
        Assert.Equal(0.2, p.X, precision: 12);
        Assert.Equal(0.2, p.Y, precision: 12);
        Assert.Equal(0.2, p.Z, precision: 12);
        Assert.Equal((byte)100, p.R);
        Assert.Equal((byte)50, p.G);
        Assert.Equal((byte)85, p.B);
        Assert.True(result.HasColor);
    }

    [Fact]
    public void Points_in_different_voxels_are_kept_separate_in_first_seen_order()
    {
        var cloud = new PointCloud(
            [
                PointRecord.Uncolored(0.1, 0.1, 0.1),
                PointRecord.Uncolored(5.1, 0.1, 0.1),
                PointRecord.Uncolored(0.3, 0.3, 0.3),
                PointRecord.Uncolored(0.1, 5.1, 0.1),
            ],
            hasColor: false);

        var result = _downsampler.Downsample(cloud, voxelSize: 1.0);

        Assert.Equal(3, result.Count);
        Assert.Equal(0.2, result.Points[0].X, precision: 12);
        Assert.Equal(5.1, result.Points[1].X, precision: 12);
        Assert.Equal(5.1, result.Points[2].Y, precision: 12);
        Assert.False(result.HasColor);
    }

    [Fact]
    public void Dense_regular_grid_reduces_to_expected_voxel_count()
    {
        // 20 x 20 x 20 lattice with spacing 0.1 spans [0, 1.9]; voxels of 0.5 give ceil(2/0.5)=4 cells per axis.
        var points = new List<PointRecord>();
        for (var x = 0; x < 20; x++)
        {
            for (var y = 0; y < 20; y++)
            {
                for (var z = 0; z < 20; z++)
                {
                    points.Add(PointRecord.Uncolored(x * 0.1, y * 0.1, z * 0.1));
                }
            }
        }

        var result = _downsampler.Downsample(new PointCloud(points, false), voxelSize: 0.5);

        Assert.Equal(64, result.Count);
    }

    [Fact]
    public void Downsampled_points_stay_within_original_bounds()
    {
        var random = new Random(42);
        var points = Enumerable.Range(0, 5_000)
            .Select(_ => PointRecord.Uncolored(random.NextDouble() * 10, random.NextDouble() * 3, random.NextDouble() - 100))
            .ToList();
        var original = BoundingBox.FromPoints(points);

        var result = _downsampler.Downsample(new PointCloud(points, false), voxelSize: 0.7);

        Assert.InRange(result.Count, 2, points.Count - 1);
        Assert.All(result.Points, p => Assert.True(original.Contains(p.X, p.Y, p.Z)));
    }

    [Fact]
    public void Tiny_voxel_size_keeps_every_distinct_point()
    {
        var points = Enumerable.Range(0, 100).Select(i => PointRecord.Uncolored(i, -i, i * 2)).ToList();

        var result = _downsampler.Downsample(new PointCloud(points, false), voxelSize: 0.01);

        Assert.Equal(points, result.Points);
    }

    [Fact]
    public void Empty_cloud_is_returned_unchanged()
    {
        var empty = new PointCloud([], false);

        Assert.Same(empty, _downsampler.Downsample(empty, 1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_invalid_voxel_sizes(double voxelSize)
    {
        var cloud = new PointCloud([PointRecord.Uncolored(0, 0, 0)], false);

        Assert.Throws<ArgumentOutOfRangeException>(() => _downsampler.Downsample(cloud, voxelSize));
    }
}
