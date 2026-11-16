using PointCloudViewer.Core;
using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Parsing;

namespace PointCloudViewer.Tests.Parsing;

public class XyzParserTests
{
    private readonly XyzParser _parser = new();

    [Fact]
    public async Task Parses_xyz_only_lines_as_uncoloured_points()
    {
        var result = await _parser.ParseAsync("0 0 0\n1.5 -2.25 3e2\n");

        Assert.Equal("xyz", result.Format);
        Assert.False(result.Cloud.HasColor);
        Assert.Equal(2, result.Cloud.Count);
        Assert.Equal(PointRecord.Uncolored(1.5, -2.25, 300), result.Cloud.Points[1]);
        Assert.Equal(0, result.SkippedLines);
    }

    [Fact]
    public async Task Parses_rgb_columns_and_clamps_out_of_range_values()
    {
        var result = await _parser.ParseAsync("1 2 3 255 128 0\n4 5 6 300 -5 12.6\n");

        Assert.True(result.Cloud.HasColor);
        Assert.Equal(new PointRecord(1, 2, 3, 255, 128, 0), result.Cloud.Points[0]);
        Assert.Equal(new PointRecord(4, 5, 6, 255, 0, 13), result.Cloud.Points[1]);
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("1;2;3")]
    [InlineData("1\t2\t3")]
    [InlineData("   1    2  3   ")]
    public async Task Accepts_common_separators(string line)
    {
        var result = await _parser.ParseAsync(line);

        Assert.Equal(PointRecord.Uncolored(1, 2, 3), Assert.Single(result.Cloud.Points));
    }

    [Fact]
    public async Task Ignores_extra_intensity_column_when_fewer_than_six_values()
    {
        var result = await _parser.ParseAsync("1 2 3 0.75\n");

        Assert.False(result.Cloud.HasColor);
        Assert.Equal(PointRecord.Uncolored(1, 2, 3), Assert.Single(result.Cloud.Points));
    }

    [Fact]
    public async Task Skips_comments_and_blank_lines_without_counting_them_as_errors()
    {
        var result = await _parser.ParseAsync("# header comment\n\n// another\n1 2 3\n   \n");

        Assert.Single(result.Cloud.Points);
        Assert.Equal(0, result.SkippedLines);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Skips_malformed_lines_and_reports_line_numbers()
    {
        const string content = """
            X Y Z
            1 2 3
            1 2
            4 five 6
            NaN 0 0
            7 8 9 1 2 x
            10 11 12
            """;

        var result = await _parser.ParseAsync(content);

        Assert.Equal(2, result.Cloud.Count);
        Assert.Equal(5, result.SkippedLines);
        Assert.Collection(
            result.Warnings,
            w => Assert.StartsWith("Line 1:", w),
            w => Assert.StartsWith("Line 3:", w),
            w => Assert.StartsWith("Line 4:", w),
            w => Assert.StartsWith("Line 5:", w),
            w => Assert.StartsWith("Line 6:", w));
    }

    [Fact]
    public async Task Caps_the_number_of_warnings_but_counts_every_skipped_line()
    {
        var content = string.Concat(Enumerable.Repeat("bad line\n", 50)) + "1 2 3\n";

        var result = await _parser.ParseAsync(content, new ParseOptions { MaxWarnings = 5 });

        Assert.Equal(50, result.SkippedLines);
        Assert.Equal(5, result.Warnings.Count);
    }

    [Fact]
    public async Task Throws_when_no_valid_points_exist()
    {
        var ex = await Assert.ThrowsAsync<InvalidPointCloudException>(() => _parser.ParseAsync("hello\nworld\n"));

        Assert.Contains("No valid points", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Throws_when_point_limit_is_exceeded()
    {
        var ex = await Assert.ThrowsAsync<PointLimitExceededException>(
            () => _parser.ParseAsync("1 1 1\n2 2 2\n3 3 3\n", new ParseOptions { MaxPoints = 2 }));

        Assert.Equal(2, ex.Limit);
    }

    [Fact]
    public async Task Uses_invariant_culture_for_decimals()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var result = await _parser.ParseAsync("1.5 2.5 3.5\n");

            Assert.Equal(PointRecord.Uncolored(1.5, 2.5, 3.5), Assert.Single(result.Cloud.Points));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
