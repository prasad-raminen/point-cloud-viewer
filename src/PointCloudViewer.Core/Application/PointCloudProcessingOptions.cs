using System.ComponentModel.DataAnnotations;

namespace PointCloudViewer.Core.Application;

/// <summary>Bound from the <c>PointCloud</c> configuration section.</summary>
public sealed class PointCloudProcessingOptions
{
    public const string SectionName = "PointCloud";

    /// <summary>Hard cap on points per imported dataset.</summary>
    [Range(1, 50_000_000)]
    public int MaxPoints { get; set; } = 5_000_000;

    /// <summary>Fixed LOD voxel size in dataset units; leave unset to derive it from <see cref="LodTargetPoints"/>.</summary>
    [Range(1e-6, 1e6)]
    public double? LodVoxelSize { get; set; }

    /// <summary>Approximate point budget for the automatically sized LOD.</summary>
    [Range(1_000, 10_000_000)]
    public int LodTargetPoints { get; set; } = 250_000;

    /// <summary>Maximum per-line parse warnings kept with a dataset.</summary>
    [Range(0, 1_000)]
    public int MaxWarnings { get; set; } = 20;
}
