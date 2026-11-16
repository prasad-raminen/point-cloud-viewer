using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PointCloudViewer.Api.Contracts;
using PointCloudViewer.Api.Options;
using PointCloudViewer.Core.Application;
using PointCloudViewer.Core.Processing;

namespace PointCloudViewer.Api.Controllers;

/// <summary>Upload, list, inspect, stream and delete point cloud datasets.</summary>
[ApiController]
[Route("api/clouds")]
public sealed class PointCloudsController(IPointCloudService service, IOptions<UploadOptions> uploadOptions) : ControllerBase
{
    public const string CountHeader = "X-Point-Count";
    public const string TotalCountHeader = "X-Point-Total-Count";
    public const string StrideHeader = "X-Point-Stride";
    public const string OriginHeader = "X-Point-Origin";
    public const string SamplingHeader = "X-Point-Sampling";

    private const int DefaultMaxPoints = 1_000_000;
    private const int MaxPointsCeiling = 5_000_000;
    private static readonly string[] Attributes = ["x", "y", "z", "r", "g", "b"];

    /// <summary>Uploads and processes an ASCII .xyz or .ply point cloud.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<PointCloudDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PointCloudDto>> Upload([FromForm] UploadPointCloudRequest request, CancellationToken cancellationToken)
    {
        var file = request.File!;
        if (file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Empty file", detail: "The uploaded file is empty.");
        }

        var maxBytes = uploadOptions.Value.MaxFileSizeBytes;
        if (file.Length > maxBytes)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "File too large",
                detail: $"The file is {file.Length:N0} bytes; the limit is {maxBytes:N0} bytes.");
        }

        await using var stream = file.OpenReadStream();
        var metadata = await service.ImportAsync(stream, file.FileName, request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = metadata.Id }, PointCloudDto.From(metadata));
    }

    /// <summary>Lists all datasets, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PointCloudDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PointCloudDto>> List(CancellationToken cancellationToken)
    {
        var all = await service.ListAsync(cancellationToken);
        return [.. all.Select(PointCloudDto.From)];
    }

    /// <summary>Gets metadata (counts, bounds, centroid, LOD info) for one dataset.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PointCloudDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PointCloudDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var metadata = await service.GetAsync(id, cancellationToken);
        return metadata is null ? NotFoundProblem(id) : PointCloudDto.From(metadata);
    }

    /// <summary>
    /// Returns up to <paramref name="maxPoints"/> points as interleaved little-endian Float32
    /// <c>[x, y, z, r, g, b]</c> (positions relative to the dataset origin, colours 0..1).
    /// </summary>
    /// <param name="id">Dataset id.</param>
    /// <param name="maxPoints">Point budget (1 - 5,000,000). The most detailed level that fits is returned.</param>
    /// <param name="format"><c>binary</c> (default, application/octet-stream) or <c>json</c>.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("{id:guid}/points")]
    [ProducesResponseType(typeof(PointSetDto), StatusCodes.Status200OK, "application/octet-stream", "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPoints(
        Guid id,
        [FromQuery] int maxPoints = DefaultMaxPoints,
        [FromQuery] PointFormat format = PointFormat.Binary,
        CancellationToken cancellationToken = default)
    {
        if (maxPoints is < 1 or > MaxPointsCeiling)
        {
            ModelState.AddModelError(nameof(maxPoints), $"maxPoints must be between 1 and {MaxPointsCeiling:N0}.");
            return ValidationProblem(ModelState);
        }

        var set = await service.GetPointsAsync(id, maxPoints, cancellationToken);
        if (set is null)
        {
            return NotFoundProblem(id);
        }

        if (format == PointFormat.Json)
        {
            return Ok(new PointSetDto(
                set.Count,
                set.TotalPointCount,
                PointBufferCodec.FloatsPerPoint,
                Attributes,
                set.Sampling,
                Vec3Dto.From(set.Origin),
                PointBufferCodec.DecodeToFloats(set.Buffer)));
        }

        var headers = Response.Headers;
        headers[CountHeader] = set.Count.ToString(CultureInfo.InvariantCulture);
        headers[TotalCountHeader] = set.TotalPointCount.ToString(CultureInfo.InvariantCulture);
        headers[StrideHeader] = PointBufferCodec.FloatsPerPoint.ToString(CultureInfo.InvariantCulture);
        headers[OriginHeader] = string.Create(CultureInfo.InvariantCulture, $"{set.Origin.X:R},{set.Origin.Y:R},{set.Origin.Z:R}");
        headers[SamplingHeader] = set.Sampling.ToString();
        return File(set.Buffer, "application/octet-stream");
    }

    /// <summary>Deletes a dataset and all of its stored files.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFoundProblem(id);

    private ObjectResult NotFoundProblem(Guid id) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Point cloud not found",
        detail: $"No point cloud with id '{id}' exists.");
}
