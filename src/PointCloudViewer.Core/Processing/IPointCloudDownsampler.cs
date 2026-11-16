using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Processing;

/// <summary>Produces a reduced level-of-detail version of a point cloud.</summary>
public interface IPointCloudDownsampler
{
    /// <param name="cloud">Source cloud.</param>
    /// <param name="voxelSize">Cell edge length in the cloud's units (must be &gt; 0).</param>
    PointCloud Downsample(PointCloud cloud, double voxelSize);
}
