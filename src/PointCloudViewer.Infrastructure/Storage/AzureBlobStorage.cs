using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PointCloudViewer.Core.Storage;

namespace PointCloudViewer.Infrastructure.Storage;

/// <summary>
/// <see cref="IBlobStorage"/> backed by an Azure Blob Storage container.
/// </summary>
/// <remarks>
/// Enabled with <c>Storage:Provider=AzureBlob</c>. Works against Azurite locally
/// (<c>UseDevelopmentStorage=true</c>). Note: this provider compiles and follows the
/// Azure SDK's documented API, but the automated test suite only exercises the local disk
/// provider; validate it against Azurite or a real account before relying on it.
/// </remarks>
public sealed class AzureBlobStorage : IBlobStorage
{
    private readonly BlobContainerClient _container;
    private readonly Lazy<Task> _ensureContainer;

    public AzureBlobStorage(BlobContainerClient container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _ensureContainer = new Lazy<Task>(() => _container.CreateIfNotExistsAsync(PublicAccessType.None));
    }

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        await _ensureContainer.Value.ConfigureAwait(false);
        var blob = _container.GetBlobClient(BlobKey.Validate(key));
        var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } };
        await blob.UploadAsync(BinaryData.FromBytes(content), options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        await _ensureContainer.Value.ConfigureAwait(false);
        try
        {
            var result = await _container.GetBlobClient(BlobKey.Validate(key))
                .DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return result.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        await _ensureContainer.Value.ConfigureAwait(false);
        try
        {
            return await _container.GetBlobClient(BlobKey.Validate(key))
                .OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<string> ListAsync(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _ensureContainer.Value.ConfigureAwait(false);
        BlobKey.Validate(prefix, allowTrailingSlash: true);
        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return item.Name;
        }
    }

    public async Task<int> DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var deleted = 0;
        await foreach (var key in ListAsync(prefix, cancellationToken).ConfigureAwait(false))
        {
            var response = await _container.DeleteBlobIfExistsAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.Value)
            {
                deleted++;
            }
        }

        return deleted;
    }
}
