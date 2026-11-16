using System.Reflection;
using System.Text.Json.Serialization;
using PointCloudViewer.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Point Cloud Viewer API", Version = "v1" });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
    {
        o.IncludeXmlComments(xml);
    }
});
builder.Services.AddPointCloudViewer(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(ServiceCollectionExtensions.CorsPolicy);
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

/// <summary>Entry point marker so WebApplicationFactory can locate the API assembly.</summary>
public partial class Program;
