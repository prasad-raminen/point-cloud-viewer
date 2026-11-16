using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Application;

public enum StoredLevel
{
    Full,
    Lod,
}

/// <summary>Persistence for dataset metadata and encoded point buffers.</summary>
public interface IPointCloudRepository
{
    Task SaveAsync(PointCloudMetadata metadata, byte[] fullBuffer, byte[]? lodBuffer, CancellationToken cancellationToken = default);

    Task<PointCloudMetadata?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PointCloudMetadata>> ListAsync(CancellationToken cancellationToken = default);

    Task<byte[]?> ReadPointsAsync(Guid id, StoredLevel level, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
