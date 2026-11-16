namespace PointCloudViewer.Core.Domain;

/// <summary>Axis-aligned bounding box in world coordinates.</summary>
public readonly record struct BoundingBox(Vec3 Min, Vec3 Max)
{
    /// <summary>
    /// An "inverted" box that contains nothing. Including any point into it yields a
    /// degenerate box around that point, which makes it a convenient fold seed.
    /// </summary>
    public static BoundingBox Empty { get; } = new(
        new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
        new Vec3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    public Vec3 Size => IsEmpty ? Vec3.Zero : Max - Min;

    public Vec3 Center => IsEmpty ? Vec3.Zero : (Min + Max) / 2.0;

    /// <summary>Length of the box diagonal; useful for camera framing.</summary>
    public double Diagonal
    {
        get
        {
            var s = Size;
            return Math.Sqrt((s.X * s.X) + (s.Y * s.Y) + (s.Z * s.Z));
        }
    }

    public BoundingBox Include(double x, double y, double z) => new(
        new Vec3(Math.Min(Min.X, x), Math.Min(Min.Y, y), Math.Min(Min.Z, z)),
        new Vec3(Math.Max(Max.X, x), Math.Max(Max.Y, y), Math.Max(Max.Z, z)));

    public BoundingBox Include(BoundingBox other) =>
        other.IsEmpty ? this : IsEmpty ? other : new(Vec3.Min(Min, other.Min), Vec3.Max(Max, other.Max));

    public bool Contains(double x, double y, double z) =>
        !IsEmpty && x >= Min.X && x <= Max.X && y >= Min.Y && y <= Max.Y && z >= Min.Z && z <= Max.Z;

    public static BoundingBox FromPoints(IEnumerable<PointRecord> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var box = Empty;
        foreach (var p in points)
        {
            box = box.Include(p.X, p.Y, p.Z);
        }

        return box;
    }
}
