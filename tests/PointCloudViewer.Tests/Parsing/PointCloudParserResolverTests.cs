using PointCloudViewer.Core;
using PointCloudViewer.Core.Parsing;

namespace PointCloudViewer.Tests.Parsing;

public class PointCloudParserResolverTests
{
    private readonly PointCloudParserResolver _resolver = new([new XyzParser(), new PlyParser()]);

    [Theory]
    [InlineData("scan.xyz", "xyz")]
    [InlineData("SCAN.XYZ", "xyz")]
    [InlineData("export.csv", "xyz")]
    [InlineData("bunny.ply", "ply")]
    public void Resolves_parser_by_extension_case_insensitively(string fileName, string expected)
    {
        Assert.Equal(expected, _resolver.Resolve(fileName).Format);
    }

    [Theory]
    [InlineData("scan.las")]
    [InlineData("noextension")]
    public void Throws_for_unknown_extensions(string fileName)
    {
        var ex = Assert.Throws<UnsupportedPointCloudFormatException>(() => _resolver.Resolve(fileName));

        Assert.Contains(".ply", ex.Message, StringComparison.Ordinal);
    }
}
