using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PointCloudViewer.Api.Contracts;
using PointCloudViewer.Api.Hosting;

namespace PointCloudViewer.Tests.Integration;

public sealed class UploadLimitTests(SmallUploadLimitFactory factory) : IClassFixture<SmallUploadLimitFactory>
{
    [Fact]
    public async Task Upload_larger_than_configured_limit_returns_413_problem()
    {
        var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[200]);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "file", "big.xyz");

        using var response = await client.PostAsync(new Uri("/api/clouds", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("File too large", problem!.Title);
    }
}

public sealed class DemoSeedingTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    [Fact]
    public async Task Demo_scene_is_seeded_into_an_empty_store_at_startup()
    {
        var client = factory.CreateClient();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        List<PointCloudDto>? list;
        do
        {
            list = await client.GetFromJsonAsync<List<PointCloudDto>>("/api/clouds", json, timeout.Token);
            if (list is { Count: > 0 })
            {
                break;
            }

            await Task.Delay(100, timeout.Token);
        }
        while (true);

        var demo = Assert.Single(list);
        Assert.Equal(DemoDataSeeder.DemoName, demo.Name);
        Assert.True(demo.IsSample);
        Assert.True(demo.HasColor);
        Assert.True(demo.PointCount > 1_000);
    }
}
