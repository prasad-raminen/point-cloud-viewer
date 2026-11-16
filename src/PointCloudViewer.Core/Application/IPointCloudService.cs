using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Application;

/// <summary>Describes a dataset being imported from an already-parsed cloud (e.g. generated demo data).</summary>
public sealed record ImportDescriptor(string Name, string SourceFileName, string Format, bool IsSample = false);

/// <summary>Use cases for managing and serving point cloud datasets.</summary>
public interface IPointCloudService
{
    /// <summary>Parses an uploaded file, builds its LOD and persists it.</summary>
    /// <exception cref="PointCloudException">The file is unsupported, malformed or too large.</exception>
    Task<PointCloudMetadata> ImportAsync(Stream content, string fileName, string? displayName, CancellationToken cancellationToken = default);

    /// <summary>Persists an in-memory cloud (used for demo data).</summary>
    Task<PointCloudMetadata> ImportAsync(PointCloud cloud, ImportDescriptor descriptor, CancellationToken cancellationToken = default);

    /// <summary>All datasets, newest first.</summary>
    Task<IReadOnlyList<PointCloudMetadata>> ListAsync(CancellationToken cancellationToken = default);

    Task<PointCloudMetadata?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns at most <paramref name="maxPoints"/> encoded points, choosing the most detailed stored
    /// level that fits the budget and decimating the LOD if even that is too large.
    /// </summary>
    Task<PointSet?> GetPointsAsync(Guid id, int maxPoints, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
