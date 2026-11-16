using System.ComponentModel.DataAnnotations;

namespace PointCloudViewer.Api.Options;

/// <summary>Bound from the <c>Upload</c> configuration section.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    /// <summary>Largest accepted upload in bytes (default 200 MB).</summary>
    [Range(1, long.MaxValue)]
    public long MaxFileSizeBytes { get; set; } = 200L * 1024 * 1024;
}

/// <summary>Bound from the <c>DemoData</c> configuration section.</summary>
public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    /// <summary>Seed a synthetic scene at startup when the store is empty.</summary>
    public bool Enabled { get; set; } = true;

    [Range(0.01, 10)]
    public double Density { get; set; } = 1.0;

    public int Seed { get; set; } = 7;
}

/// <summary>Bound from the <c>Cors</c> configuration section.</summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}
