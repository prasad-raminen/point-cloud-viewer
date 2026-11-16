using PointCloudViewer.Core.Domain;
using PointCloudViewer.Core.Processing;

namespace PointCloudViewer.Tests.Processing;

public class PointBufferCodecTests
{
    [Fact]
    public void Encodes_interleaved_float32_relative_to_origin_with_normalised_colour()
    {
        var points = new[] { new PointRecord(101, 202, 303, 255, 0, 51) };

        var bytes = PointBufferCodec.Encode(points, origin: new Vec3(100, 200, 300));
        var floats = PointBufferCodec.DecodeToFloats(bytes);

        Assert.Equal(PointBufferCodec.BytesPerPoint, bytes.Length);
        Assert.Equal([1f, 2f, 3f, 1f, 0f, 0.2f], floats);
    }

    [Fact]
    public void Uses_little_endian_byte_order()
    {
        var bytes = PointBufferCodec.Encode([PointRecord.Uncolored(1, 0, 0)], Vec3.Zero);

        // 1.0f == 0x3F800000 -> little-endian bytes 00 00 80 3F
        Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0x3F }, bytes[..4]);
    }

    [Fact]
    public void Origin_offset_preserves_precision_for_georeferenced_coordinates()
    {
        var origin = new Vec3(512_345.0, 5_412_987.0, 0);
        var points = new[] { PointRecord.Uncolored(512_345.123, 5_412_987.456, 12.789) };

        var floats = PointBufferCodec.DecodeToFloats(PointBufferCodec.Encode(points, origin));

        Assert.Equal(0.123, floats[0], precision: 5);
        Assert.Equal(0.456, floats[1], precision: 5);
    }

    [Fact]
    public void Decimate_picks_evenly_spaced_records()
    {
        var points = Enumerable.Range(0, 10).Select(i => PointRecord.Uncolored(i, 0, 0)).ToList();
        var bytes = PointBufferCodec.Encode(points, Vec3.Zero);

        var decimated = PointBufferCodec.DecodeToFloats(PointBufferCodec.Decimate(bytes, 5));

        var xs = Enumerable.Range(0, 5).Select(i => decimated[i * PointBufferCodec.FloatsPerPoint]);
        Assert.Equal([0f, 2f, 4f, 6f, 8f], xs);
    }

    [Fact]
    public void Decimate_returns_same_buffer_when_already_small_enough()
    {
        var bytes = PointBufferCodec.Encode([PointRecord.Uncolored(0, 0, 0)], Vec3.Zero);

        Assert.Same(bytes, PointBufferCodec.Decimate(bytes, 10));
    }

    [Fact]
    public void Rejects_buffers_that_are_not_whole_records()
    {
        Assert.Throws<InvalidDataException>(() => PointBufferCodec.CountPoints(25));
    }
}
