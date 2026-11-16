using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using PointCloudViewer.Core.Storage;

namespace PointCloudViewer.Tests.Fakes;

/// <summary>Thread-safe in-memory <see cref="IBlobStorage"/> for fast service tests.</summary>
internal sealed class InMemoryBlobStorage : IBlobStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Keys => [.. _blobs.Keys];

    public Task WriteAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        _blobs[BlobKey.Validate(key)] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.TryGetValue(key, out var value) ? value : null);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(_blobs.TryGetValue(key, out var value) ? new MemoryStream(value, writable: false) : null);

    public async IAsyncEnumerable<string> ListAsync(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in _blobs.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            yield return key;
        }

        await Task.CompletedTask;
    }

    public Task<int> DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var removed = _blobs.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Count(k => _blobs.TryRemove(k, out _));
        return Task.FromResult(removed);
    }

    public void Corrupt(string key) => _blobs[key] = "{ not json"u8.ToArray();
}
