using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Tests.Domain;

public class CloudStatisticsTests
{
    [Fact]
    public void Empty_input_produces_zero_count_and_empty_bounds()
    {
        var stats = CloudStatistics.Compute([]);

        Assert.Equal(0, stats.PointCount);
        Assert.True(stats.Bounds.IsEmpty);
        Assert.Equal(Vec3.Zero, stats.Centroid);
    }

    [Fact]
    public void Centroid_is_the_arithmetic_mean_of_positions()
    {
        var points = new[]
        {
            PointRecord.Uncolored(0, 0, 0),
            PointRecord.Uncolored(4, 0, 0),
            PointRecord.Uncolored(0, 4, 0),
            PointRecord.Uncolored(0, 0, 8),
        };

        var stats = CloudStatistics.Compute(points);

        Assert.Equal(4, stats.PointCount);
        Assert.Equal(new Vec3(1, 1, 2), stats.Centroid);
        Assert.Equal(new Vec3(0, 0, 0), stats.Bounds.Min);
        Assert.Equal(new Vec3(4, 4, 8), stats.Bounds.Max);
    }

    [Fact]
    public void Centroid_stays_accurate_for_large_georeferenced_coordinates()
    {
        // UTM-like eastings/northings: naive float accumulation would drift noticeably here.
        const double baseX = 512_345.678;
        const double baseY = 5_412_987.123;
        var points = Enumerable.Range(0, 200_000)
            .Select(i => PointRecord.Uncolored(baseX + (i % 2 == 0 ? 0.01 : -0.01), baseY, 100))
            .ToList();

        var stats = CloudStatistics.Compute(points);

        Assert.Equal(baseX, stats.Centroid.X, precision: 6);
        Assert.Equal(baseY, stats.Centroid.Y, precision: 6);
    }
}
