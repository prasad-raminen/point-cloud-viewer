using System.Buffers.Binary;
using PointCloudViewer.Core.Domain;

namespace PointCloudViewer.Core.Processing;

/// <summary>
/// Encodes points into the compact wire/storage format consumed by the web client:
/// interleaved little-endian Float32 records of <c>[x, y, z, r, g, b]</c>.
/// </summary>
/// <remarks>
/// Positions are written relative to an <c>origin</c> (the bounding box centre) so that
/// georeferenced coordinates keep sub-millimetre precision in 32-bit floats; the origin is
/// returned with the dataset metadata. Colour channels are normalised to 0..1.
/// </remarks>
public static class PointBufferCodec
{
    public const int FloatsPerPoint = 6;
    public const int BytesPerPoint = FloatsPerPoint * sizeof(float);

    public static byte[] Encode(IReadOnlyList<PointRecord> points, Vec3 origin)
    {
        ArgumentNullException.ThrowIfNull(points);
        var buffer = new byte[points.Count * BytesPerPoint];
        var span = buffer.AsSpan();
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var record = span.Slice(i * BytesPerPoint, BytesPerPoint);
            BinaryPrimitives.WriteSingleLittleEndian(record[0..], (float)(p.X - origin.X));
            BinaryPrimitives.WriteSingleLittleEndian(record[4..], (float)(p.Y - origin.Y));
            BinaryPrimitives.WriteSingleLittleEndian(record[8..], (float)(p.Z - origin.Z));
            BinaryPrimitives.WriteSingleLittleEndian(record[12..], p.R / 255f);
            BinaryPrimitives.WriteSingleLittleEndian(record[16..], p.G / 255f);
            BinaryPrimitives.WriteSingleLittleEndian(record[20..], p.B / 255f);
        }

        return buffer;
    }

    /// <summary>Reads an encoded buffer back into a flat float array (used for JSON responses and tests).</summary>
    public static float[] DecodeToFloats(ReadOnlySpan<byte> buffer)
    {
        EnsureWholeRecords(buffer.Length);
        var floats = new float[buffer.Length / sizeof(float)];
        for (var i = 0; i < floats.Length; i++)
        {
            floats[i] = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(i * sizeof(float), sizeof(float)));
        }

        return floats;
    }

    public static int CountPoints(long byteLength)
    {
        EnsureWholeRecords(byteLength);
        return checked((int)(byteLength / BytesPerPoint));
    }

    /// <summary>
    /// Uniformly decimates an encoded buffer to at most <paramref name="maxPoints"/> records by
    /// picking evenly spaced indices. Returns the input unchanged when it is already small enough.
    /// </summary>
    public static byte[] Decimate(byte[] buffer, int maxPoints)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 1);
        var count = CountPoints(buffer.Length);
        if (count <= maxPoints)
        {
            return buffer;
        }

        var result = new byte[maxPoints * BytesPerPoint];
        for (var i = 0; i < maxPoints; i++)
        {
            var source = (int)((long)i * count / maxPoints);
            Buffer.BlockCopy(buffer, source * BytesPerPoint, result, i * BytesPerPoint, BytesPerPoint);
        }

        return result;
    }

    private static void EnsureWholeRecords(long byteLength)
    {
        if (byteLength % BytesPerPoint != 0)
        {
            throw new InvalidDataException($"Point buffer length {byteLength} is not a multiple of {BytesPerPoint} bytes.");
        }
    }
}
