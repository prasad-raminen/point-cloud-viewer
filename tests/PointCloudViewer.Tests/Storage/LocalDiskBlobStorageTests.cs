using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PointCloudViewer.Infrastructure.Storage;

namespace PointCloudViewer.Tests.Storage;

public sealed class LocalDiskBlobStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pcv-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalDiskBlobStorage _storage;

    public LocalDiskBlobStorageTests()
    {
        _storage = new LocalDiskBlobStorage(_root, NullLogger<LocalDiskBlobStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Write_then_read_round_trips_content()
    {
        var content = Encoding.UTF8.GetBytes("hello cloud");

        await _storage.WriteAsync("clouds/a/metadata.json", content, "application/json");
        var read = await _storage.ReadAsync("clouds/a/metadata.json");

        Assert.Equal(content, read);
    }

    [Fact]
    public async Task Write_overwrites_existing_blob()
    {
        await _storage.WriteAsync("x.bin", new byte[] { 1, 2, 3 }, "application/octet-stream");
        await _storage.WriteAsync("x.bin", new byte[] { 9 }, "application/octet-stream");

        Assert.Equal(new byte[] { 9 }, await _storage.ReadAsync("x.bin"));
    }

    [Fact]
    public async Task Missing_blob_returns_null()
    {
        Assert.Null(await _storage.ReadAsync("clouds/missing/full.bin"));
        Assert.Null(await _storage.OpenReadAsync("clouds/missing/full.bin"));
    }

    [Fact]
    public async Task OpenRead_streams_the_content()
    {
        await _storage.WriteAsync("s.bin", new byte[] { 4, 5, 6 }, "application/octet-stream");

        await using var stream = await _storage.OpenReadAsync("s.bin");
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);

        Assert.Equal(new byte[] { 4, 5, 6 }, copy.ToArray());
    }

    [Fact]
    public async Task List_and_delete_by_prefix_only_affect_matching_keys()
    {
        await _storage.WriteAsync("clouds/a/metadata.json", new byte[] { 1 }, "application/json");
        await _storage.WriteAsync("clouds/a/full.bin", new byte[] { 1 }, "application/octet-stream");
        await _storage.WriteAsync("clouds/b/metadata.json", new byte[] { 1 }, "application/json");

        var listed = await ToListAsync(_storage.ListAsync("clouds/a/"));
        var deleted = await _storage.DeleteByPrefixAsync("clouds/a/");
        var remaining = await ToListAsync(_storage.ListAsync("clouds/"));

        Assert.Equal(["clouds/a/full.bin", "clouds/a/metadata.json"], listed.Order(StringComparer.Ordinal));
        Assert.Equal(2, deleted);
        Assert.Equal(["clouds/b/metadata.json"], remaining);
        Assert.False(Directory.Exists(Path.Combine(_root, "clouds", "a")));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("clouds/../../escape.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("clouds//double")]
    [InlineData("clouds/with space")]
    [InlineData("clouds\\backslash")]
    public async Task Rejects_keys_that_could_escape_the_root(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.WriteAsync(key, new byte[] { 1 }, "text/plain"));
    }

    private static async Task<List<string>> ToListAsync(IAsyncEnumerable<string> source)
    {
        var list = new List<string>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
