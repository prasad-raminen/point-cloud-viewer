using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PointCloudViewer.Core.Storage;

namespace PointCloudViewer.Infrastructure.Storage;

/// <summary>Stores blobs as files under a root directory. Writes are atomic (temp file + rename).</summary>
public sealed partial class LocalDiskBlobStorage : IBlobStorage
{
    private readonly string _root;
    private readonly ILogger<LocalDiskBlobStorage> _logger;

    public LocalDiskBlobStorage(string rootPath, ILogger<LocalDiskBlobStorage> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _root = Path.GetFullPath(rootPath);
        _logger = logger;
        Directory.CreateDirectory(_root);
    }

    public string RootPath => _root;

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public async IAsyncEnumerable<string> ListAsync(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        BlobKey.Validate(prefix, allowTrailingSlash: true);
        if (!Directory.Exists(_root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.Contains(".tmp-", StringComparison.Ordinal))
            {
                continue;
            }

            var key = Path.GetRelativePath(_root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return key;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<int> DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var deleted = 0;
        await foreach (var key in ListAsync(prefix, cancellationToken).ConfigureAwait(false))
        {
            File.Delete(ResolvePath(key));
            deleted++;
        }

        // Remove the directories left behind by prefix-scoped deletes (e.g. clouds/{id}/).
        var directory = Path.Combine(_root, prefix.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
        if (prefix.EndsWith('/') && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }

        return deleted;
    }

    private string ResolvePath(string key)
    {
        BlobKey.Validate(key);
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Blob key '{key}' escapes the storage root.", nameof(key));
        }

        return full;
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            LogTempCleanupFailed(_logger, ex, path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove temporary file {Path}")]
    private static partial void LogTempCleanupFailed(ILogger logger, Exception exception, string path);
}
