using Microsoft.Extensions.Options;
using PointCloudViewer.Api.Options;
using PointCloudViewer.Core.Application;
using PointCloudViewer.Core.Samples;

namespace PointCloudViewer.Api.Hosting;

/// <summary>
/// Seeds a synthetic site scan when the store is empty so the viewer has something to show on
/// first run. Runs in the background so it never delays the API from accepting requests.
/// </summary>
public sealed partial class DemoDataSeeder(
    IPointCloudService service,
    IOptions<DemoDataOptions> options,
    ILogger<DemoDataSeeder> logger) : BackgroundService
{
    public const string DemoName = "Demo site scan";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        try
        {
            var existing = await service.ListAsync(stoppingToken);
            if (existing.Count > 0)
            {
                return;
            }

            var cloud = await Task.Run(() => SyntheticSceneGenerator.Generate(settings.Seed, settings.Density), stoppingToken);
            var metadata = await service.ImportAsync(
                cloud,
                new ImportDescriptor(DemoName, "synthetic-site.generated", "synthetic", IsSample: true),
                stoppingToken);
            LogSeeded(logger, metadata.Id, metadata.PointCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; nothing to do.
        }
#pragma warning disable CA1031 // Demo seeding is best-effort and must never crash the host.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogSeedFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded demo point cloud {Id} with {PointCount} points")]
    private static partial void LogSeeded(ILogger logger, Guid id, long pointCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seeding demo data failed")]
    private static partial void LogSeedFailed(ILogger logger, Exception exception);
}
