using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Parsing;
using PointCloudViewer.Core.Processing;

namespace PointCloudViewer.Core.Application;

public sealed partial class PointCloudService : IPointCloudService
{
    private const int MaxNameLength = 120;

    private readonly IPointCloudParserResolver _parsers;
    private readonly IPointCloudDownsampler _downsampler;
    private readonly IPointCloudRepository _repository;
    private readonly PointCloudProcessingOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<PointCloudService> _logger;

    public PointCloudService(
        IPointCloudParserResolver parsers,
        IPointCloudDownsampler downsampler,
        IPointCloudRepository repository,
        IOptions<PointCloudProcessingOptions> options,
        TimeProvider clock,
        ILogger<PointCloudService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _parsers = parsers;
        _downsampler = downsampler;
        _repository = repository;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PointCloudMetadata> ImportAsync(Stream content, string fileName, string? displayName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var safeFileName = Path.GetFileName(fileName);
        var parser = _parsers.Resolve(safeFileName);
        var parseOptions = new ParseOptions { MaxPoints = _options.MaxPoints, MaxWarnings = _options.MaxWarnings };

        var counting = new CountingStream(content);
        var parsed = await parser.ParseAsync(counting, parseOptions, cancellationToken).ConfigureAwait(false);

        var name = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(safeFileName) : displayName;
        var descriptor = new ImportDescriptor(name, safeFileName, parsed.Format);
        return await PersistAsync(parsed.Cloud, descriptor, parsed, counting.BytesRead, cancellationToken).ConfigureAwait(false);
    }

    public Task<PointCloudMetadata> ImportAsync(PointCloud cloud, ImportDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cloud);
        ArgumentNullException.ThrowIfNull(descriptor);
        if (cloud.Count == 0)
        {
            throw new InvalidPointCloudException("Cannot import an empty point cloud.");
        }

        if (cloud.Count > _options.MaxPoints)
        {
            throw new PointLimitExceededException(_options.MaxPoints);
        }

        return PersistAsync(cloud, descriptor, parsed: null, sourceSize: 0, cancellationToken);
    }

    public async Task<IReadOnlyList<PointCloudMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. all.OrderByDescending(m => m.CreatedAtUtc).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public Task<PointCloudMetadata?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<PointSet?> GetPointsAsync(Guid id, int maxPoints, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 1);
        var metadata = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var (level, sampling) = metadata.PointCount <= maxPoints || !metadata.HasLod
            ? (StoredLevel.Full, PointSampling.Full)
            : (StoredLevel.Lod, PointSampling.VoxelLod);

        var buffer = await _repository.ReadPointsAsync(id, level, cancellationToken).ConfigureAwait(false);
        if (buffer is null)
        {
            LogMissingBuffer(_logger, id, level);
            return null;
        }

        if (PointBufferCodec.CountPoints(buffer.Length) > maxPoints)
        {
            buffer = PointBufferCodec.Decimate(buffer, maxPoints);
            sampling = PointSampling.Decimated;
        }

        return new PointSet(buffer, PointBufferCodec.CountPoints(buffer.Length), sampling, metadata.Origin, metadata.PointCount);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (removed)
        {
            LogRemoved(_logger, id);
        }

        return removed;
    }

    private async Task<PointCloudMetadata> PersistAsync(
        PointCloud cloud, ImportDescriptor descriptor, ParseResult? parsed, long sourceSize, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var stats = CloudStatistics.Compute(cloud.Points);
        var origin = stats.Bounds.Center;

        var voxelSize = _options.LodVoxelSize ?? VoxelSizeEstimator.EstimateForTarget(cloud.Points, _options.LodTargetPoints);
        var lod = voxelSize is { } size ? _downsampler.Downsample(cloud, size) : null;

        // A LOD that does not actually reduce the cloud is pointless to store.
        if (lod is not null && lod.Count >= cloud.Count)
        {
            lod = null;
            voxelSize = null;
        }

        var metadata = new PointCloudMetadata
        {
            Id = Guid.NewGuid(),
            Name = Truncate(descriptor.Name.Trim(), MaxNameLength),
            SourceFileName = descriptor.SourceFileName,
            Format = descriptor.Format,
            PointCount = stats.PointCount,
            LodPointCount = lod?.Count ?? stats.PointCount,
            LodVoxelSize = voxelSize,
            HasColor = cloud.HasColor,
            Bounds = stats.Bounds,
            Centroid = stats.Centroid,
            Origin = origin,
            SkippedLines = parsed?.SkippedLines ?? 0,
            Warnings = parsed?.Warnings ?? [],
            CreatedAtUtc = _clock.GetUtcNow(),
            SourceSizeBytes = sourceSize,
            IsSample = descriptor.IsSample,
        };

        var fullBuffer = PointBufferCodec.Encode(cloud.Points, origin);
        var lodBuffer = lod is null ? null : PointBufferCodec.Encode(lod.Points, origin);
        await _repository.SaveAsync(metadata, fullBuffer, lodBuffer, cancellationToken).ConfigureAwait(false);

        LogImported(_logger, metadata.Id, metadata.Name, metadata.PointCount, metadata.LodPointCount, metadata.SkippedLines, stopwatch.ElapsedMilliseconds);
        return metadata;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Imported point cloud {Id} '{Name}': {PointCount} points, LOD {LodPointCount}, {SkippedLines} skipped line(s) in {ElapsedMs} ms")]
    private static partial void LogImported(ILogger logger, Guid id, string name, long pointCount, long lodPointCount, int skippedLines, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed point cloud {Id}")]
    private static partial void LogRemoved(ILogger logger, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Point buffer {Level} for dataset {Id} is missing")]
    private static partial void LogMissingBuffer(ILogger logger, Guid id, StoredLevel level);
}
