using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PointCloudViewer.Tests.Integration;

/// <summary>
/// Hosts the real API in memory with an isolated temporary storage directory.
/// Demo seeding is off by default so tests start from an empty store.
/// </summary>
public class PointCloudApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings;

    public PointCloudApiFactory()
        : this(new Dictionary<string, string?>())
    {
    }

    protected PointCloudApiFactory(Dictionary<string, string?> overrides)
    {
        StorageRoot = Path.Combine(Path.GetTempPath(), "pcv-api-tests", Guid.NewGuid().ToString("N"));
        _settings = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "LocalDisk",
            ["Storage:LocalDisk:RootPath"] = StorageRoot,
            ["DemoData:Enabled"] = "false",
            ["PointCloud:LodTargetPoints"] = "1000",
        };
        foreach (var (key, value) in overrides)
        {
            _settings[key] = value;
        }
    }

    public string StorageRoot { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }
}

/// <summary>Factory with a tiny upload limit for exercising 413 handling.</summary>
public sealed class SmallUploadLimitFactory() : PointCloudApiFactory(new() { ["Upload:MaxFileSizeBytes"] = "64" });

/// <summary>Factory with demo seeding enabled at low density.</summary>
public sealed class SeededApiFactory() : PointCloudApiFactory(new() { ["DemoData:Enabled"] = "true", ["DemoData:Density"] = "0.1" });
