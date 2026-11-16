using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PointCloudViewer.Core.Storage;
using PointCloudViewer.Infrastructure.Storage;

namespace PointCloudViewer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the <see cref="IBlobStorage"/> implementation selected by <c>Storage:Provider</c>.</summary>
    public static IServiceCollection AddBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(
                o => o.Provider != StorageProvider.AzureBlob || !string.IsNullOrWhiteSpace(o.AzureBlob.ConnectionString),
                "Storage:AzureBlob:ConnectionString is required when Storage:Provider is AzureBlob.")
            .ValidateOnStart();

        services.AddSingleton<IBlobStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return options.Provider switch
            {
                StorageProvider.AzureBlob => CreateAzure(options.AzureBlob),
                _ => CreateLocalDisk(sp, options.LocalDisk),
            };
        });

        return services;
    }

    private static LocalDiskBlobStorage CreateLocalDisk(IServiceProvider sp, LocalDiskStorageOptions options)
    {
        var env = sp.GetRequiredService<IHostEnvironment>();
        var root = Path.IsPathRooted(options.RootPath)
            ? options.RootPath
            : Path.Combine(env.ContentRootPath, options.RootPath);
        return new LocalDiskBlobStorage(root, sp.GetRequiredService<ILogger<LocalDiskBlobStorage>>());
    }

    private static AzureBlobStorage CreateAzure(AzureBlobStorageOptions options)
    {
        // A connection string covers Azurite and key-based access. A production deployment would
        // typically use a managed identity instead: new BlobContainerClient(containerUri,
        // new DefaultAzureCredential()) from the Azure.Identity package.
        var container = new BlobContainerClient(options.ConnectionString, options.ContainerName);
        return new AzureBlobStorage(container);
    }
}
