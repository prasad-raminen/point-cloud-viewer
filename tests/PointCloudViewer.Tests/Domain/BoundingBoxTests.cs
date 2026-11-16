using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Tests.Domain;

public class BoundingBoxTests
{
    [Fact]
    public void Empty_box_reports_empty_and_zero_size()
    {
        var box = BoundingBox.Empty;

        Assert.True(box.IsEmpty);
        Assert.Equal(Vec3.Zero, box.Size);
        Assert.Equal(Vec3.Zero, box.Center);
        Assert.Equal(0, box.Diagonal);
    }

    [Fact]
    public void Including_a_single_point_yields_degenerate_box_at_that_point()
    {
        var box = BoundingBox.Empty.Include(1.5, -2, 3);

        Assert.False(box.IsEmpty);
        Assert.Equal(new Vec3(1.5, -2, 3), box.Min);
        Assert.Equal(new Vec3(1.5, -2, 3), box.Max);
        Assert.Equal(Vec3.Zero, box.Size);
    }

    [Fact]
    public void FromPoints_computes_min_max_size_center_and_diagonal()
    {
        var points = new[]
        {
            PointRecord.Uncolored(0, 0, 0),
            PointRecord.Uncolored(2, -1, 4),
            PointRecord.Uncolored(-2, 3, 1),
        };

        var box = BoundingBox.FromPoints(points);

        Assert.Equal(new Vec3(-2, -1, 0), box.Min);
        Assert.Equal(new Vec3(2, 3, 4), box.Max);
        Assert.Equal(new Vec3(4, 4, 4), box.Size);
        Assert.Equal(new Vec3(0, 1, 2), box.Center);
        Assert.Equal(Math.Sqrt(48), box.Diagonal, precision: 12);
    }

    [Fact]
    public void Union_with_empty_box_is_identity()
    {
        var box = new BoundingBox(new Vec3(0, 0, 0), new Vec3(1, 1, 1));

        Assert.Equal(box, box.Include(BoundingBox.Empty));
        Assert.Equal(box, BoundingBox.Empty.Include(box));
    }

    [Fact]
    public void Union_of_two_boxes_covers_both()
    {
        var a = new BoundingBox(new Vec3(0, 0, 0), new Vec3(1, 1, 1));
        var b = new BoundingBox(new Vec3(-1, 0.5, 2), new Vec3(0.5, 3, 5));

        var union = a.Include(b);

        Assert.Equal(new Vec3(-1, 0, 0), union.Min);
        Assert.Equal(new Vec3(1, 3, 5), union.Max);
    }

    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(1, 1, 1, true)]
    [InlineData(0.5, 0.5, 0.5, true)]
    [InlineData(1.0001, 0.5, 0.5, false)]
    [InlineData(0.5, -0.0001, 0.5, false)]
    public void Contains_is_inclusive_of_faces(double x, double y, double z, bool expected)
    {
        var box = new BoundingBox(new Vec3(0, 0, 0), new Vec3(1, 1, 1));

        Assert.Equal(expected, box.Contains(x, y, z));
    }
}
