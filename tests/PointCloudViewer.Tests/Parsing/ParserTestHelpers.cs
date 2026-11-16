using System.Text;
using PointCloudViewer.Core.Parsing;

namespace PointCloudViewer.Tests.Parsing;

internal static class ParserTestHelpers
{
    public static Task<ParseResult> ParseAsync(this IPointCloudParser parser, string content, ParseOptions? options = null)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return parser.ParseAsync(stream, options ?? ParseOptions.Default);
    }
}
