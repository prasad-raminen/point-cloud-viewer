using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Storage;

namespace PointCloudViewer.Core.Application;

/// <summary>
/// Stores each dataset under <c>clouds/{id}/</c> as <c>full.bin</c>, optional <c>lod.bin</c> and
/// <c>metadata.json</c>. Metadata is written last so a dataset only becomes visible once its
/// point buffers are complete.
/// </summary>
public sealed partial class BlobPointCloudRepository(IBlobStorage storage, ILogger<BlobPointCloudRepository> logger)
    : IPointCloudRepository
{
    private const string Root = "clouds/";
    private const string MetadataFile = "metadata.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task SaveAsync(PointCloudMetadata metadata, byte[] fullBuffer, byte[]? lodBuffer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(fullBuffer);

        await storage.WriteAsync(Key(metadata.Id, "full.bin"), fullBuffer, "application/octet-stream", cancellationToken).ConfigureAwait(false);
        if (lodBuffer is not null)
        {
            await storage.WriteAsync(Key(metadata.Id, "lod.bin"), lodBuffer, "application/octet-stream", cancellationToken).ConfigureAwait(false);
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(MetadataDocument.From(metadata), JsonOptions);
        await storage.WriteAsync(Key(metadata.Id, MetadataFile), json, "application/json", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PointCloudMetadata?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bytes = await storage.ReadAsync(Key(id, MetadataFile), cancellationToken).ConfigureAwait(false);
        return bytes is null ? null : Deserialize(bytes, id.ToString());
    }

    public async Task<IReadOnlyList<PointCloudMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<PointCloudMetadata>();
        await foreach (var key in storage.ListAsync(Root, cancellationToken).ConfigureAwait(false))
        {
            if (!key.EndsWith("/" + MetadataFile, StringComparison.Ordinal))
            {
                continue;
            }

            var bytes = await storage.ReadAsync(key, cancellationToken).ConfigureAwait(false);
            if (bytes is not null && Deserialize(bytes, key) is { } metadata)
            {
                result.Add(metadata);
            }
        }

        return result;
    }

    public Task<byte[]?> ReadPointsAsync(Guid id, StoredLevel level, CancellationToken cancellationToken = default) =>
        storage.ReadAsync(Key(id, level == StoredLevel.Lod ? "lod.bin" : "full.bin"), cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Remove metadata first so a partially deleted dataset is never listed.
        var deleted = await storage.DeleteByPrefixAsync(Key(id, MetadataFile), cancellationToken).ConfigureAwait(false);
        deleted += await storage.DeleteByPrefixAsync(Prefix(id), cancellationToken).ConfigureAwait(false);
        return deleted > 0;
    }

    private static string Prefix(Guid id) => Root + id.ToString("D", CultureInfo.InvariantCulture) + "/";

    private static string Key(Guid id, string file) => Prefix(id) + file;

    private PointCloudMetadata? Deserialize(byte[] bytes, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<MetadataDocument>(bytes, JsonOptions)?.ToDomain();
        }
        catch (JsonException ex)
        {
            LogCorruptMetadata(logger, ex, source);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring unreadable dataset metadata at {Source}")]
    private static partial void LogCorruptMetadata(ILogger logger, Exception exception, string source);

    /// <summary>Storage schema, decoupled from the domain type so either can evolve independently.</summary>
    private sealed record MetadataDocument(
        int SchemaVersion,
        Guid Id,
        string Name,
        string SourceFileName,
        string Format,
        long PointCount,
        long LodPointCount,
        double? LodVoxelSize,
        bool HasColor,
        double[] BoundsMin,
        double[] BoundsMax,
        double[] Centroid,
        double[] Origin,
        int SkippedLines,
        string[] Warnings,
        DateTimeOffset CreatedAtUtc,
        long SourceSizeBytes,
        bool IsSample)
    {
        public static MetadataDocument From(PointCloudMetadata m) => new(
            1, m.Id, m.Name, m.SourceFileName, m.Format, m.PointCount, m.LodPointCount, m.LodVoxelSize, m.HasColor,
            ToArray(m.Bounds.Min), ToArray(m.Bounds.Max), ToArray(m.Centroid), ToArray(m.Origin),
            m.SkippedLines, [.. m.Warnings], m.CreatedAtUtc, m.SourceSizeBytes, m.IsSample);

        public PointCloudMetadata ToDomain() => new()
        {
            Id = Id,
            Name = Name,
            SourceFileName = SourceFileName,
            Format = Format,
            PointCount = PointCount,
            LodPointCount = LodPointCount,
            LodVoxelSize = LodVoxelSize,
            HasColor = HasColor,
            Bounds = new BoundingBox(ToVec(BoundsMin), ToVec(BoundsMax)),
            Centroid = ToVec(Centroid),
            Origin = ToVec(Origin),
            SkippedLines = SkippedLines,
            Warnings = Warnings ?? [],
            CreatedAtUtc = CreatedAtUtc,
            SourceSizeBytes = SourceSizeBytes,
            IsSample = IsSample,
        };

        private static double[] ToArray(Vec3 v) => [v.X, v.Y, v.Z];

        private static Vec3 ToVec(double[] a) =>
            a is { Length: 3 } ? new Vec3(a[0], a[1], a[2]) : throw new JsonException("Expected a 3-component vector.");
    }
}
