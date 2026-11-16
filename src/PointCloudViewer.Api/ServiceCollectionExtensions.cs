using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using PointCloudViewer.Api.Controllers;
using PointCloudViewer.Api.ErrorHandling;
using PointCloudViewer.Api.Hosting;
using PointCloudViewer.Api.Options;
using PointCloudViewer.Core.Application;
using PointCloudViewer.Core.Parsing;
using PointCloudViewer.Core.Processing;
using PointCloudViewer.Infrastructure;

namespace PointCloudViewer.Api;

internal static class ServiceCollectionExtensions
{
    public const string CorsPolicy = "client";

    /// <summary>Slack on top of the file limit for multipart boundaries and form fields.</summary>
    private const long MultipartOverheadBytes = 1024 * 1024;

    public static IServiceCollection AddPointCloudViewer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PointCloudProcessingOptions>()
            .Bind(configuration.GetSection(PointCloudProcessingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<UploadOptions>()
            .Bind(configuration.GetSection(UploadOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<DemoDataOptions>()
            .Bind(configuration.GetSection(DemoDataOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Upload size limits: the controller enforces the exact file limit with a 413 problem
        // response; these transport limits are a backstop against oversized request bodies.
        services.AddOptions<FormOptions>().Configure<IOptions<UploadOptions>>((form, upload) =>
            form.MultipartBodyLengthLimit = upload.Value.MaxFileSizeBytes + MultipartOverheadBytes);
        services.AddOptions<KestrelServerOptions>().Configure<IOptions<UploadOptions>>((kestrel, upload) =>
            kestrel.Limits.MaxRequestBodySize = upload.Value.MaxFileSizeBytes + MultipartOverheadBytes);

        // Core services
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPointCloudParser, XyzParser>();
        services.AddSingleton<IPointCloudParser, PlyParser>();
        services.AddSingleton<IPointCloudParserResolver, PointCloudParserResolver>();
        services.AddSingleton<IPointCloudDownsampler, VoxelGridDownsampler>();
        services.AddSingleton<IPointCloudRepository, BlobPointCloudRepository>();
        services.AddSingleton<IPointCloudService, PointCloudService>();

        services.AddBlobStorage(configuration);
        services.AddHostedService<DemoDataSeeder>();

        // Error handling: RFC 7807 problem details for everything.
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<PointCloudExceptionHandler>();

        var origins = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins ?? [];
        services.AddCors(o => o.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "DELETE")
            .WithExposedHeaders(
                PointCloudsController.CountHeader,
                PointCloudsController.TotalCountHeader,
                PointCloudsController.StrideHeader,
                PointCloudsController.OriginHeader,
                PointCloudsController.SamplingHeader,
                "Location")));

        services.AddHealthChecks();
        return services;
    }
}
