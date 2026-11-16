using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Samples;

/// <summary>
/// Generates a deterministic, coloured demo scan of a small site: rolling terrain, a road,
/// a paved plaza, a four-storey building with windows and a rooftop unit, and trees.
/// </summary>
/// <remarks>
/// Uses the Z-up, metre-based convention common to LiDAR / survey data. With the default
/// density the scene has roughly 150k points. Output is identical for the same seed.
/// </remarks>
public static class SyntheticSceneGenerator
{
    private const double HalfExtent = 60;
    private const double BuildingHalfX = 12;
    private const double BuildingHalfY = 8;
    private const double BuildingHeight = 14;
    private const double FloorHeight = 3.5;
    private const double RoadMinY = -32;
    private const double RoadMaxY = -25;

    public static PointCloud Generate(int seed = 7, double density = 1.0)
    {
        if (!double.IsFinite(density) || density <= 0 || density > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(density), density, "Density must be in (0, 10].");
        }

        var random = new Random(seed);
        var points = new List<PointRecord>(capacity: (int)(160_000 * density));
        var spacingScale = 1.0 / Math.Sqrt(density);

        AddTerrain(points, random, 0.35 * spacingScale);
        AddBuilding(points, random, 0.25 * spacingScale);
        AddTrees(points, random, density);

        return new PointCloud(points, hasColor: true);
    }

    /// <summary>Ground height at a location (flattened under the plaza and road).</summary>
    public static double TerrainHeight(double x, double y)
    {
        var natural = (1.6 * Math.Sin(x * 0.05) * Math.Cos(y * 0.04)) + (0.6 * Math.Sin((x + y) * 0.11)) + (0.02 * x);
        var plazaWeight = SmoothStep(6, 0, DistanceOutside(x, y, BuildingHalfX + 6, BuildingHalfY + 6));
        var roadWeight = SmoothStep(4, 0, Math.Max(RoadMinY - y, y - RoadMaxY));
        var flatten = Math.Max(plazaWeight, roadWeight);
        var flatHeight = plazaWeight >= roadWeight ? 0.0 : 0.02 * x;
        return Lerp(natural, flatHeight, flatten);
    }

    private static void AddTerrain(List<PointRecord> points, Random random, double spacing)
    {
        for (var x = -HalfExtent; x <= HalfExtent; x += spacing)
        {
            for (var y = -HalfExtent; y <= HalfExtent; y += spacing)
            {
                var px = x + Jitter(random, spacing * 0.45);
                var py = y + Jitter(random, spacing * 0.45);
                if (Math.Abs(px) < BuildingHalfX && Math.Abs(py) < BuildingHalfY)
                {
                    continue; // under the building footprint
                }

                var pz = TerrainHeight(px, py) + Jitter(random, 0.02);
                var (r, g, b) = GroundColor(px, py, pz, random);
                points.Add(new PointRecord(px, py, pz, r, g, b));
            }
        }
    }

    private static (byte R, byte G, byte B) GroundColor(double x, double y, double z, Random random)
    {
        var noise = Jitter(random, 12);
        if (y is >= RoadMinY and <= RoadMaxY)
        {
            var centre = (RoadMinY + RoadMaxY) / 2;
            var isMarking = Math.Abs(y - centre) < 0.12 && ((int)Math.Floor((x + HalfExtent) / 3) % 2 == 0);
            return isMarking ? Rgb(235, 225, 170, noise) : Rgb(62, 64, 68, noise);
        }

        if (DistanceOutside(x, y, BuildingHalfX + 5, BuildingHalfY + 5) <= 0)
        {
            return Rgb(168, 166, 160, noise);
        }

        // Grass gets lighter on higher ground, with occasional bare-earth patches.
        var t = Math.Clamp((z + 2.5) / 5.0, 0, 1);
        var patch = Math.Sin(x * 0.21) * Math.Cos(y * 0.17) > 0.82;
        return patch
            ? Rgb(120, 98, 70, noise)
            : Rgb(Lerp(52, 110, t), Lerp(96, 150, t), Lerp(40, 70, t), noise);
    }

    private static void AddBuilding(List<PointRecord> points, Random random, double spacing)
    {
        // Four walls: (fixed axis value, along-axis half length, normal direction)
        AddWall(points, random, spacing, alongX: true, fixedCoord: -BuildingHalfY, outward: -1);
        AddWall(points, random, spacing, alongX: true, fixedCoord: BuildingHalfY, outward: 1);
        AddWall(points, random, spacing, alongX: false, fixedCoord: -BuildingHalfX, outward: -1);
        AddWall(points, random, spacing, alongX: false, fixedCoord: BuildingHalfX, outward: 1);

        // Flat roof with a small parapet-height rooftop unit.
        for (var x = -BuildingHalfX; x <= BuildingHalfX; x += spacing * 1.2)
        {
            for (var y = -BuildingHalfY; y <= BuildingHalfY; y += spacing * 1.2)
            {
                var noise = Jitter(random, 8);
                points.Add(new PointRecord(x + Jitter(random, 0.05), y + Jitter(random, 0.05), BuildingHeight + Jitter(random, 0.01), (byte)(118 + noise), (byte)(118 + noise), (byte)(124 + noise)));
            }
        }

        AddBox(points, random, spacing, new Vec3(2, -3, BuildingHeight), new Vec3(6, 1, BuildingHeight + 2), (182, 186, 192));
    }

    private static void AddWall(List<PointRecord> points, Random random, double spacing, bool alongX, double fixedCoord, int outward)
    {
        var halfLength = alongX ? BuildingHalfX : BuildingHalfY;
        for (var s = -halfLength; s <= halfLength; s += spacing)
        {
            for (var z = 0.0; z <= BuildingHeight; z += spacing)
            {
                var isWindow = IsWindow(s, z, halfLength);
                var inset = isWindow ? -0.15 * outward : 0;
                var depth = fixedCoord + inset + Jitter(random, 0.01);
                var along = s + Jitter(random, 0.03);
                var height = z + Jitter(random, 0.03);
                var noise = Jitter(random, 8);
                var (r, g, b) = isWindow ? Rgb(70, 98, 128, noise) : Rgb(212, 200, 178, noise);
                points.Add(alongX
                    ? new PointRecord(along, depth, height, r, g, b)
                    : new PointRecord(depth, along, height, r, g, b));
            }
        }
    }

    private static bool IsWindow(double along, double z, double halfLength)
    {
        var floorZ = z % FloorHeight;
        if (z < 1.0 || floorZ < 1.0 || floorZ > 2.6)
        {
            return false;
        }

        var bayWidth = 3.0;
        var local = (along + halfLength) % bayWidth;
        return along > -halfLength + 1 && along < halfLength - 1 && local > 0.7 && local < 2.3;
    }

    private static void AddBox(List<PointRecord> points, Random random, double spacing, Vec3 min, Vec3 max, (int R, int G, int B) color)
    {
        for (var x = min.X; x <= max.X; x += spacing)
        {
            for (var y = min.Y; y <= max.Y; y += spacing)
            {
                points.Add(Colored(x, y, max.Z, color, random));
            }

            for (var z = min.Z; z <= max.Z; z += spacing)
            {
                points.Add(Colored(x, min.Y, z, color, random));
                points.Add(Colored(x, max.Y, z, color, random));
            }
        }

        for (var y = min.Y; y <= max.Y; y += spacing)
        {
            for (var z = min.Z; z <= max.Z; z += spacing)
            {
                points.Add(Colored(min.X, y, z, color, random));
                points.Add(Colored(max.X, y, z, color, random));
            }
        }
    }

    private static void AddTrees(List<PointRecord> points, Random random, double density)
    {
        var placed = 0;
        var attempts = 0;
        while (placed < 16 && attempts++ < 500)
        {
            var x = (random.NextDouble() * 2 - 1) * (HalfExtent - 5);
            var y = (random.NextDouble() * 2 - 1) * (HalfExtent - 5);
            var clearOfPlaza = DistanceOutside(x, y, BuildingHalfX + 8, BuildingHalfY + 8) > 0;
            var clearOfRoad = y < RoadMinY - 4 || y > RoadMaxY + 4;
            if (!clearOfPlaza || !clearOfRoad)
            {
                continue;
            }

            AddTree(points, random, x, y, density);
            placed++;
        }
    }

    private static void AddTree(List<PointRecord> points, Random random, double x, double y, double density)
    {
        var ground = TerrainHeight(x, y);
        var trunkHeight = 2.5 + random.NextDouble();
        var canopyRadius = 2.0 + (random.NextDouble() * 1.4);
        var trunkPoints = (int)(160 * density);
        var canopyPoints = (int)(900 * density);

        for (var i = 0; i < trunkPoints; i++)
        {
            var angle = random.NextDouble() * Math.Tau;
            var z = ground + (random.NextDouble() * trunkHeight);
            points.Add(Colored(x + (0.25 * Math.Cos(angle)), y + (0.25 * Math.Sin(angle)), z, (96, 70, 46), random));
        }

        var centreZ = ground + trunkHeight + (canopyRadius * 0.7);
        var tint = random.Next(-15, 15);
        for (var i = 0; i < canopyPoints; i++)
        {
            // Uniform direction on a sphere, radius biased towards the surface (as a scanner sees it).
            var u = (random.NextDouble() * 2) - 1;
            var theta = random.NextDouble() * Math.Tau;
            var ring = Math.Sqrt(1 - (u * u));
            var radius = canopyRadius * (0.75 + (0.25 * Math.Cbrt(random.NextDouble())));
            var px = x + (radius * ring * Math.Cos(theta));
            var py = y + (radius * ring * Math.Sin(theta));
            var pz = centreZ + (radius * u * 0.85);
            var shade = (u + 1) / 2;
            points.Add(Colored(px, py, pz, ((int)Lerp(34, 70, shade) + tint, (int)Lerp(90, 150, shade) + tint, (int)Lerp(36, 62, shade)), random));
        }
    }

    private static PointRecord Colored(double x, double y, double z, (int R, int G, int B) color, Random random)
    {
        var noise = Jitter(random, 8);
        var (r, g, b) = Rgb(color.R, color.G, color.B, noise);
        return new PointRecord(x + Jitter(random, 0.02), y + Jitter(random, 0.02), z + Jitter(random, 0.02), r, g, b);
    }

    private static (byte R, byte G, byte B) Rgb(double r, double g, double b, double noise) =>
        (ToByte(r + noise), ToByte(g + noise), ToByte(b + noise));

    private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

    private static double Jitter(Random random, double amplitude) => ((random.NextDouble() * 2) - 1) * amplitude;

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    /// <summary>Hermite smoothstep from <paramref name="edge0"/> to <paramref name="edge1"/> (edges may be descending).</summary>
    private static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - (2 * t));
    }

    /// <summary>Distance from (x, y) to an origin-centred rectangle; zero or negative inside it.</summary>
    private static double DistanceOutside(double x, double y, double halfX, double halfY)
    {
        var dx = Math.Abs(x) - halfX;
        var dy = Math.Abs(y) - halfY;
        return dx <= 0 && dy <= 0 ? Math.Max(dx, dy) : Math.Sqrt((Math.Max(dx, 0) * Math.Max(dx, 0)) + (Math.Max(dy, 0) * Math.Max(dy, 0)));
    }
}
