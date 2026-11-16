using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Samples;

namespace PointCloudViewer.Tests.Samples;

public class SyntheticSceneGeneratorTests
{
    [Fact]
    public void Generates_a_coloured_scene_of_reasonable_size()
    {
        var cloud = SyntheticSceneGenerator.Generate();

        Assert.True(cloud.HasColor);
        Assert.InRange(cloud.Count, 100_000, 250_000);
    }

    [Fact]
    public void Is_deterministic_for_a_given_seed()
    {
        var a = SyntheticSceneGenerator.Generate(seed: 3, density: 0.2);
        var b = SyntheticSceneGenerator.Generate(seed: 3, density: 0.2);
        var c = SyntheticSceneGenerator.Generate(seed: 4, density: 0.2);

        Assert.Equal(a.Points, b.Points);
        Assert.NotEqual(a.Points, c.Points);
    }

    [Fact]
    public void Scene_contains_terrain_extent_and_building_roof()
    {
        var stats = CloudStatistics.Compute(SyntheticSceneGenerator.Generate(density: 0.3).Points);

        Assert.True(stats.Bounds.Size.X > 110, "terrain should span ~120 m");
        Assert.True(stats.Bounds.Size.Y > 110, "terrain should span ~120 m");
        Assert.InRange(stats.Bounds.Max.Z, 15.5, 17); // rooftop unit on a 14 m building
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void Rejects_out_of_range_density(double density)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SyntheticSceneGenerator.Generate(density: density));
    }
}
