using System.ComponentModel.DataAnnotations;
using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Api.Contracts;

/// <summary>Multipart upload form for <c>POST /api/clouds</c>.</summary>
public sealed class UploadPointCloudRequest
{
    /// <summary>ASCII .xyz (also .txt/.csv/.pts) or ASCII .ply file.</summary>
    [Required]
    public IFormFile? File { get; set; }

    /// <summary>Optional display name; defaults to the file name without extension.</summary>
    [StringLength(120)]
    public string? Name { get; set; }
}

public sealed record Vec3Dto(double X, double Y, double Z)
{
    public static Vec3Dto From(Vec3 v) => new(v.X, v.Y, v.Z);
}

public sealed record BoundsDto(Vec3Dto Min, Vec3Dto Max, Vec3Dto Size);

/// <summary>Dataset metadata returned by the API.</summary>
public sealed record PointCloudDto(
    Guid Id,
    string Name,
    string SourceFileName,
    string Format,
    long PointCount,
    long LodPointCount,
    double? LodVoxelSize,
    bool HasColor,
    BoundsDto Bounds,
    Vec3Dto Centroid,
    Vec3Dto Origin,
    int SkippedLines,
    IReadOnlyList<string> Warnings,
    long SourceSizeBytes,
    bool IsSample,
    DateTimeOffset CreatedAtUtc)
{
    public static PointCloudDto From(PointCloudMetadata m) => new(
        m.Id,
        m.Name,
        m.SourceFileName,
        m.Format,
        m.PointCount,
        m.LodPointCount,
        m.LodVoxelSize,
        m.HasColor,
        new BoundsDto(Vec3Dto.From(m.Bounds.Min), Vec3Dto.From(m.Bounds.Max), Vec3Dto.From(m.Bounds.Size)),
        Vec3Dto.From(m.Centroid),
        Vec3Dto.From(m.Origin),
        m.SkippedLines,
        m.Warnings,
        m.SourceSizeBytes,
        m.IsSample,
        m.CreatedAtUtc);
}

/// <summary>JSON form of a point set; <see cref="Points"/> is a flat array with <see cref="Stride"/> values per point.</summary>
public sealed record PointSetDto(
    int Count,
    long TotalPointCount,
    int Stride,
    IReadOnlyList<string> Attributes,
    PointSampling Sampling,
    Vec3Dto Origin,
    float[] Points);

/// <summary>Wire formats for <c>GET /api/clouds/{id}/points</c>.</summary>
public enum PointFormat
{
    Binary,
    Json,
}
