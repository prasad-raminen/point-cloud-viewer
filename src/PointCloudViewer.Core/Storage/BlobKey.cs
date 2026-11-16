using System.Text.RegularExpressions;

namespace PointCloudViewer.Core.Storage;

/// <summary>Validation for blob keys so no implementation can be tricked into path traversal.</summary>
public static partial class BlobKey
{
    public static string Validate(string key, bool allowTrailingSlash = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var body = allowTrailingSlash && key.EndsWith('/') ? key[..^1] : key;
        var segments = body.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "." || s == ".." || !SegmentPattern().IsMatch(s)))
        {
            throw new ArgumentException($"Invalid blob key '{key}'.", nameof(key));
        }

        return key;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
