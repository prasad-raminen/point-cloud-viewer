namespace PointCloudViewer.Core.Domain;

/// <summary>Everything known about a stored dataset, without its points.</summary>
public sealed record PointCloudMetadata
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string SourceFileName { get; init; }

    /// <summary>Source format identifier ("xyz", "ply" or "synthetic").</summary>
    public required string Format { get; init; }

    public required long PointCount { get; init; }

    /// <summary>Points in the voxel level-of-detail version (equals <see cref="PointCount"/> when none was built).</summary>
    public required long LodPointCount { get; init; }

    /// <summary>Voxel edge length used for the LOD, or <c>null</c> when the full cloud was small enough.</summary>
    public double? LodVoxelSize { get; init; }

    public required bool HasColor { get; init; }

    public required BoundingBox Bounds { get; init; }

    public required Vec3 Centroid { get; init; }

    /// <summary>Offset subtracted from every position in the binary point buffers.</summary>
    public required Vec3 Origin { get; init; }

    public int SkippedLines { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public long SourceSizeBytes { get; init; }

    public bool IsSample { get; init; }

    public bool HasLod => LodVoxelSize is not null;
}
