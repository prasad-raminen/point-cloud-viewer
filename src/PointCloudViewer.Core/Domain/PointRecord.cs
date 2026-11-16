namespace PointCloudViewer.Core.Domain;

/// <summary>A single point: world-space position plus an 8-bit RGB colour.</summary>
/// <remarks>
/// Points without colour information are stored as white (255, 255, 255) and the owning
/// <see cref="PointCloud"/> reports <see cref="PointCloud.HasColor"/> = <c>false</c>.
/// </remarks>
public readonly record struct PointRecord(double X, double Y, double Z, byte R, byte G, byte B)
{
    public const byte DefaultChannel = 255;

    public static PointRecord Uncolored(double x, double y, double z) =>
        new(x, y, z, DefaultChannel, DefaultChannel, DefaultChannel);

    public Vec3 Position => new(X, Y, Z);
}
