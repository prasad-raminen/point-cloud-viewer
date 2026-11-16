namespace PointCloudViewer.Core.Storage;

/// <summary>
/// Minimal key/value blob store used to persist datasets. Keys are forward-slash separated
/// relative paths made of <c>[A-Za-z0-9._-]</c> segments (e.g. <c>clouds/{id}/full.bin</c>).
/// </summary>
/// <remarks>
/// Implementations: local disk for development and containers, Azure Blob Storage for cloud
/// deployments. Keeping the contract this small makes it trivial to back with any object store.
/// </remarks>
public interface IBlobStorage
{
    /// <summary>Creates or overwrites a blob.</summary>
    Task WriteAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Reads a blob fully into memory, or returns <c>null</c> when it does not exist.</summary>
    Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Opens a blob for streaming, or returns <c>null</c> when it does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Lists keys starting with <paramref name="prefix"/>.</summary>
    IAsyncEnumerable<string> ListAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Deletes every blob whose key starts with <paramref name="prefix"/>; returns how many were removed.</summary>
    Task<int> DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
