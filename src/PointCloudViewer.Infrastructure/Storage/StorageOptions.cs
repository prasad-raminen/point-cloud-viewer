using System.ComponentModel.DataAnnotations;

namespace PointCloudViewer.Infrastructure.Storage;

public enum StorageProvider
{
    LocalDisk,
    AzureBlob,
}

/// <summary>Bound from the <c>Storage</c> configuration section.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.LocalDisk;

    public LocalDiskStorageOptions LocalDisk { get; set; } = new();

    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

public sealed class LocalDiskStorageOptions
{
    /// <summary>Directory for dataset files; relative paths resolve against the content root.</summary>
    [Required]
    public string RootPath { get; set; } = "data";
}

public sealed class AzureBlobStorageOptions
{
    /// <summary>
    /// Storage account connection string. Use <c>UseDevelopmentStorage=true</c> for Azurite locally;
    /// in Azure, supply it from Key Vault / app settings rather than committing it.
    /// </summary>
    public string? ConnectionString { get; set; }

    [Required]
    public string ContainerName { get; set; } = "point-clouds";
}
