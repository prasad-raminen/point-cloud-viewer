using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PointCloudViewer.Core;
using PointCloudViewer.Core.Application;
using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Parsing;
using PointCloudViewer.Core.Processing;
using PointCloudViewer.Tests.Fakes;

namespace PointCloudViewer.Tests.Application;

public class PointCloudServiceTests
{
    private readonly InMemoryBlobStorage _storage = new();

    [Fact]
    public async Task Import_parses_computes_statistics_and_persists_metadata()
    {
        var service = CreateService();

        var metadata = await service.ImportAsync(Text("0 0 0\n2 4 6\nbad\n"), "scan.xyz", displayName: null);

        Assert.Equal("scan", metadata.Name);
        Assert.Equal("scan.xyz", metadata.SourceFileName);
        Assert.Equal("xyz", metadata.Format);
        Assert.Equal(2, metadata.PointCount);
        Assert.Equal(new Vec3(0, 0, 0), metadata.Bounds.Min);
        Assert.Equal(new Vec3(2, 4, 6), metadata.Bounds.Max);
        Assert.Equal(new Vec3(1, 2, 3), metadata.Centroid);
        Assert.Equal(new Vec3(1, 2, 3), metadata.Origin);
        Assert.Equal(1, metadata.SkippedLines);
        Assert.Equal(16, metadata.SourceSizeBytes);
        Assert.False(metadata.HasLod);

        var reloaded = await service.GetAsync(metadata.Id);
        Assert.Equivalent(metadata, reloaded, strict: true);
    }

    [Fact]
    public async Task Import_strips_directories_from_client_supplied_file_names()
    {
        var service = CreateService();

        var metadata = await service.ImportAsync(Text("1 2 3\n"), "../../etc/evil.xyz", "  Survey  ");

        Assert.Equal("evil.xyz", metadata.SourceFileName);
        Assert.Equal("Survey", metadata.Name);
    }

    [Fact]
    public async Task Import_rejects_unsupported_extensions()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UnsupportedPointCloudFormatException>(
            () => service.ImportAsync(Text("1 2 3"), "scan.las", null));
    }

    [Fact]
    public async Task Import_builds_a_voxel_lod_when_cloud_exceeds_target()
    {
        var service = CreateService(new PointCloudProcessingOptions { LodTargetPoints = 1_000 });

        var metadata = await service.ImportAsync(GridCloud(60), new ImportDescriptor("grid", "grid", "synthetic"));

        Assert.Equal(3_600, metadata.PointCount);
        Assert.True(metadata.HasLod);
        Assert.InRange(metadata.LodPointCount, 1, 1_000);
        Assert.Contains($"clouds/{metadata.Id}/lod.bin", _storage.Keys);
    }

    [Fact]
    public async Task Import_uses_fixed_voxel_size_when_configured()
    {
        var service = CreateService(new PointCloudProcessingOptions { LodVoxelSize = 10 });

        // 60 x 60 grid with spacing 1 => 6 x 6 voxels of size 10.
        var metadata = await service.ImportAsync(GridCloud(60), new ImportDescriptor("grid", "grid", "synthetic"));

        Assert.Equal(10, metadata.LodVoxelSize);
        Assert.Equal(36, metadata.LodPointCount);
    }

    [Fact]
    public async Task Import_of_in_memory_cloud_enforces_point_limit()
    {
        var service = CreateService(new PointCloudProcessingOptions { MaxPoints = 10 });

        await Assert.ThrowsAsync<PointLimitExceededException>(
            () => service.ImportAsync(GridCloud(5), new ImportDescriptor("g", "g", "synthetic")));
    }

    [Fact]
    public async Task GetPoints_returns_full_resolution_when_it_fits_the_budget()
    {
        var service = CreateService(new PointCloudProcessingOptions { LodTargetPoints = 1_000 });
        var metadata = await service.ImportAsync(GridCloud(60), new ImportDescriptor("grid", "grid", "synthetic"));

        var points = await service.GetPointsAsync(metadata.Id, maxPoints: 10_000);

        Assert.NotNull(points);
        Assert.Equal(PointSampling.Full, points.Sampling);
        Assert.Equal(3_600, points.Count);
        Assert.Equal(3_600 * PointBufferCodec.BytesPerPoint, points.Buffer.Length);
        Assert.Equal(metadata.Origin, points.Origin);
    }

    [Fact]
    public async Task GetPoints_falls_back_to_lod_then_decimates_to_respect_budget()
    {
        var service = CreateService(new PointCloudProcessingOptions { LodTargetPoints = 1_000 });
        var metadata = await service.ImportAsync(GridCloud(60), new ImportDescriptor("grid", "grid", "synthetic"));

        var lod = await service.GetPointsAsync(metadata.Id, maxPoints: 2_000);
        var decimated = await service.GetPointsAsync(metadata.Id, maxPoints: 50);

        Assert.Equal(PointSampling.VoxelLod, lod!.Sampling);
        Assert.Equal(metadata.LodPointCount, lod.Count);
        Assert.Equal(PointSampling.Decimated, decimated!.Sampling);
        Assert.Equal(50, decimated.Count);
        Assert.Equal(3_600, decimated.TotalPointCount);
    }

    [Fact]
    public async Task GetPoints_returns_null_for_unknown_dataset()
    {
        Assert.Null(await CreateService().GetPointsAsync(Guid.NewGuid(), 100));
    }

    [Fact]
    public async Task List_returns_newest_first_and_skips_corrupt_metadata()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock: clock);
        var first = await service.ImportAsync(Text("1 1 1\n"), "first.xyz", null);
        clock.Advance(TimeSpan.FromMinutes(5));
        var second = await service.ImportAsync(Text("2 2 2\n"), "second.xyz", null);
        var broken = await service.ImportAsync(Text("3 3 3\n"), "broken.xyz", null);
        _storage.Corrupt($"clouds/{broken.Id}/metadata.json");

        var list = await service.ListAsync();

        Assert.Equal([second.Id, first.Id], list.Select(m => m.Id));
    }

    [Fact]
    public async Task Delete_removes_every_blob_of_the_dataset()
    {
        var service = CreateService(new PointCloudProcessingOptions { LodTargetPoints = 1_000 });
        var keep = await service.ImportAsync(Text("1 1 1\n"), "keep.xyz", null);
        var remove = await service.ImportAsync(GridCloud(60), new ImportDescriptor("grid", "grid", "synthetic"));

        Assert.True(await service.DeleteAsync(remove.Id));
        Assert.False(await service.DeleteAsync(remove.Id));
        Assert.Null(await service.GetAsync(remove.Id));
        Assert.All(_storage.Keys, k => Assert.StartsWith($"clouds/{keep.Id}/", k));
    }

    private PointCloudService CreateService(PointCloudProcessingOptions? options = null, TimeProvider? clock = null) => new(
        new PointCloudParserResolver([new XyzParser(), new PlyParser()]),
        new VoxelGridDownsampler(),
        new BlobPointCloudRepository(_storage, NullLogger<BlobPointCloudRepository>.Instance),
        Options.Create(options ?? new PointCloudProcessingOptions()),
        clock ?? TimeProvider.System,
        NullLogger<PointCloudService>.Instance);

    private static MemoryStream Text(string content) => new(Encoding.UTF8.GetBytes(content));

    private static PointCloud GridCloud(int size)
    {
        var points = new List<PointRecord>(size * size);
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                points.Add(PointRecord.Uncolored(x, y, 0));
            }
        }

        return new PointCloud(points, hasColor: false);
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
