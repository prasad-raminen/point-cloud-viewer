namespace PointCloudViewer.Core;

/// <summary>Base type for expected, client-caused point cloud errors.</summary>
public abstract class PointCloudException : Exception
{
    protected PointCloudException(string message)
        : base(message)
    {
    }

    protected PointCloudException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The file was recognised but its content could not be turned into a usable cloud.</summary>
public sealed class InvalidPointCloudException : PointCloudException
{
    public InvalidPointCloudException(string message)
        : base(message)
    {
    }
}

/// <summary>The file type (extension or encoding variant) is not supported.</summary>
public sealed class UnsupportedPointCloudFormatException : PointCloudException
{
    public UnsupportedPointCloudFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>The cloud contains more points than the configured import limit.</summary>
public sealed class PointLimitExceededException : PointCloudException
{
    public PointLimitExceededException(long limit)
        : base($"The point cloud exceeds the import limit of {limit:N0} points.")
    {
        Limit = limit;
    }

    public long Limit { get; }
}
