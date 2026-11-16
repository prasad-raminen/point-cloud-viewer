using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using PointCloudViewer.Api.Contracts;
using PointCloudViewer.Core.Processing;

namespace PointCloudViewer.Tests.Integration;

public sealed class PointCloudsApiTests(PointCloudApiFactory factory) : IClassFixture<PointCloudApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upload_xyz_returns_201_with_location_and_metadata()
    {
        using var response = await UploadAsync("0 0 0\n10 20 30 255 128 0\nnot a point\n", "site.xyz", name: "Site A");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PointCloudDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal($"/api/clouds/{dto.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Site A", dto.Name);
        Assert.Equal("xyz", dto.Format);
        Assert.Equal(2, dto.PointCount);
        Assert.True(dto.HasColor);
        Assert.Equal(1, dto.SkippedLines);
        Assert.Equal(new Vec3Dto(10, 20, 30), dto.Bounds.Max);
        Assert.Equal(new Vec3Dto(5, 10, 15), dto.Centroid);
    }

    [Fact]
    public async Task Upload_ascii_ply_is_parsed()
    {
        const string ply = "ply\nformat ascii 1.0\nelement vertex 2\nproperty float x\nproperty float y\nproperty float z\nend_header\n1 2 3\n4 5 6\n";

        using var response = await UploadAsync(ply, "mesh.ply");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PointCloudDto>(Json);
        Assert.Equal("ply", dto!.Format);
        Assert.Equal("mesh", dto.Name);
        Assert.False(dto.HasColor);
    }

    [Fact]
    public async Task Upload_with_unsupported_extension_returns_415_problem()
    {
        using var response = await UploadAsync("1 2 3", "scan.las");

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "Unsupported point cloud format");
    }

    [Fact]
    public async Task Upload_of_binary_ply_returns_415_problem()
    {
        using var response = await UploadAsync("ply\nformat binary_little_endian 1.0\nend_header\n", "bin.ply");

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "Unsupported point cloud format");
    }

    [Fact]
    public async Task Upload_without_any_valid_points_returns_422_problem()
    {
        using var response = await UploadAsync("hello\nworld\n", "junk.xyz");

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "Invalid point cloud");
    }

    [Fact]
    public async Task Upload_without_file_returns_400_validation_problem()
    {
        using var content = new MultipartFormDataContent { { new StringContent("x"), "name" } };

        using var response = await _client.PostAsync(new Uri("/api/clouds", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains("File", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Upload_of_empty_file_returns_400()
    {
        using var response = await UploadAsync(string.Empty, "empty.xyz");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Empty file");
    }

    [Fact]
    public async Task List_includes_uploaded_dataset()
    {
        var created = await CreateAsync("1 1 1\n", "listed.xyz");

        var list = await _client.GetFromJsonAsync<List<PointCloudDto>>("/api/clouds", Json);

        Assert.Contains(list!, d => d.Id == created.Id && d.Name == "listed");
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_problem()
    {
        using var response = await _client.GetAsync(new Uri($"/api/clouds/{Guid.NewGuid()}", UriKind.Relative));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Point cloud not found");
    }

    [Fact]
    public async Task Get_points_binary_returns_float32_buffer_with_headers()
    {
        var created = await CreateAsync("100 200 300 255 0 0\n102 204 306 0 0 255\n", "two.xyz");

        using var response = await _client.GetAsync(new Uri($"/api/clouds/{created.Id}/points", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("2", Header(response, "X-Point-Count"));
        Assert.Equal("6", Header(response, "X-Point-Stride"));
        Assert.Equal("Full", Header(response, "X-Point-Sampling"));
        var origin = Header(response, "X-Point-Origin").Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture));
        Assert.Equal([101.0, 202.0, 303.0], origin);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var floats = PointBufferCodec.DecodeToFloats(bytes);
        Assert.Equal(2 * PointBufferCodec.BytesPerPoint, bytes.Length);
        Assert.Equal([-1f, -2f, -3f, 1f, 0f, 0f, 1f, 2f, 3f, 0f, 0f, 1f], floats);
    }

    [Fact]
    public async Task Get_points_respects_max_points_using_lod_and_decimation()
    {
        var created = await CreateAsync(GridXyz(50), "grid.xyz");
        Assert.True(created.LodPointCount < created.PointCount);

        using var lod = await _client.GetAsync(new Uri($"/api/clouds/{created.Id}/points?maxPoints=1500", UriKind.Relative));
        using var small = await _client.GetAsync(new Uri($"/api/clouds/{created.Id}/points?maxPoints=10", UriKind.Relative));

        Assert.Equal("VoxelLod", Header(lod, "X-Point-Sampling"));
        Assert.Equal(created.LodPointCount.ToString(CultureInfo.InvariantCulture), Header(lod, "X-Point-Count"));
        Assert.Equal("Decimated", Header(small, "X-Point-Sampling"));
        Assert.Equal(10 * PointBufferCodec.BytesPerPoint, (await small.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Get_points_json_returns_flat_array()
    {
        var created = await CreateAsync("1 2 3\n", "one.xyz");

        var set = await _client.GetFromJsonAsync<PointSetDto>($"/api/clouds/{created.Id}/points?format=json", Json);

        Assert.NotNull(set);
        Assert.Equal(1, set.Count);
        Assert.Equal(6, set.Stride);
        Assert.Equal(["x", "y", "z", "r", "g", "b"], set.Attributes);
        Assert.Equal([0f, 0f, 0f, 1f, 1f, 1f], set.Points);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5000001")]
    public async Task Get_points_with_invalid_max_points_returns_400(string maxPoints)
    {
        var created = await CreateAsync("1 2 3\n", "bad-max.xyz");

        using var response = await _client.GetAsync(new Uri($"/api/clouds/{created.Id}/points?maxPoints={maxPoints}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_204_then_404()
    {
        var created = await CreateAsync("1 2 3\n", "doomed.xyz");
        var uri = new Uri($"/api/clouds/{created.Id}", UriKind.Relative);

        using var first = await _client.DeleteAsync(uri);
        using var second = await _client.DeleteAsync(uri);
        using var get = await _client.GetAsync(uri);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(factory.StorageRoot, "clouds", created.Id.ToString())));
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        Assert.Equal("Healthy", await _client.GetStringAsync(new Uri("/health", UriKind.Relative)));
    }

    private async Task<HttpResponseMessage> UploadAsync(string body, string fileName, string? name = null)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "file", fileName);
        if (name is not null)
        {
            content.Add(new StringContent(name), "name");
        }

        return await _client.PostAsync(new Uri("/api/clouds", UriKind.Relative), content);
    }

    private async Task<PointCloudDto> CreateAsync(string body, string fileName)
    {
        using var response = await UploadAsync(body, fileName);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PointCloudDto>(Json))!;
    }

    private static string GridXyz(int size)
    {
        var sb = new StringBuilder();
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                sb.Append(CultureInfo.InvariantCulture, $"{x} {y} 0\n");
            }
        }

        return sb.ToString();
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.Single() : throw new Xunit.Sdk.XunitException($"Missing header {name}");

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.Equal((int)status, problem!.Status);
        Assert.Equal(title, problem.Title);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
