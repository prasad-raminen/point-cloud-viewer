namespace PointCloudViewer.Core.Domain;

/// <summary>How the returned points relate to the full dataset.</summary>
public enum PointSampling
{
    /// <summary>Every point in the dataset.</summary>
    Full,

    /// <summary>The voxel-grid level-of-detail version.</summary>
    VoxelLod,

    /// <summary>Evenly decimated from the LOD to honour a client point budget.</summary>
    Decimated,
}

/// <summary>A batch of encoded points (see <c>PointBufferCodec</c>) ready to be sent to a client.</summary>
public sealed record PointSet(byte[] Buffer, int Count, PointSampling Sampling, Vec3 Origin, long TotalPointCount);
