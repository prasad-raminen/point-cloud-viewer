import { describe, expect, it } from 'vitest';
import { BYTES_PER_POINT, decodePointBuffer, parseOrigin, parseSampling } from './pointBuffer';

/** Builds a little-endian buffer exactly as the API's PointBufferCodec writes it. */
function encode(records: number[][]): ArrayBuffer {
  const buffer = new ArrayBuffer(records.length * BYTES_PER_POINT);
  const view = new DataView(buffer);
  records.forEach((record, i) =>
    record.forEach((value, j) => view.setFloat32(i * BYTES_PER_POINT + j * 4, value, true)),
  );
  return buffer;
}

describe('decodePointBuffer', () => {
  it('splits interleaved records into position and colour arrays', () => {
    const decoded = decodePointBuffer(
      encode([
        [1, 2, 3, 1, 0, 0],
        [-4, 5.5, -6, 0, 0.5, 1],
      ]),
    );

    expect(decoded.count).toBe(2);
    expect(Array.from(decoded.positions)).toEqual([1, 2, 3, -4, 5.5, -6]);
    expect(Array.from(decoded.colors)).toEqual([1, 0, 0, 0, 0.5, 1]);
  });

  it('reports the local z range of the decoded points', () => {
    const decoded = decodePointBuffer(
      encode([
        [0, 0, 4, 0, 0, 0],
        [0, 0, -2, 0, 0, 0],
        [0, 0, 1, 0, 0, 0],
      ]),
    );

    expect(decoded.zRange).toEqual([-2, 4]);
  });

  it('reads little-endian regardless of host byte order', () => {
    const buffer = new ArrayBuffer(BYTES_PER_POINT);
    // 1.0f is 0x3F800000; little-endian bytes are 00 00 80 3F.
    new Uint8Array(buffer).set([0x00, 0x00, 0x80, 0x3f]);

    expect(decodePointBuffer(buffer).positions[0]).toBe(1);
  });

  it('carries metadata and defaults sensibly', () => {
    const origin = { x: 500_000, y: 4_000_000, z: 12 };
    const withMeta = decodePointBuffer(encode([[0, 0, 0, 1, 1, 1]]), {
      origin,
      sampling: 'VoxelLod',
      totalCount: 10,
    });
    const defaults = decodePointBuffer(new ArrayBuffer(0));

    expect(withMeta.origin).toEqual(origin);
    expect(withMeta.sampling).toBe('VoxelLod');
    expect(withMeta.totalCount).toBe(10);
    expect(defaults.count).toBe(0);
    expect(defaults.totalCount).toBe(0);
    expect(defaults.zRange).toEqual([0, 0]);
  });

  it('rejects buffers that are not a whole number of records', () => {
    expect(() => decodePointBuffer(new ArrayBuffer(25))).toThrow(/multiple of 24/);
  });
});

describe('parseOrigin', () => {
  it('parses the X-Point-Origin header', () => {
    expect(parseOrigin('512345.25,-12.5,0')).toEqual({ x: 512345.25, y: -12.5, z: 0 });
  });

  it.each([null, undefined, '', '1,2', 'a,b,c', '1,2,3,4'])('falls back to zero for %s', (value) => {
    expect(parseOrigin(value)).toEqual({ x: 0, y: 0, z: 0 });
  });
});

describe('parseSampling', () => {
  it('accepts known values and defaults to Full', () => {
    expect(parseSampling('VoxelLod')).toBe('VoxelLod');
    expect(parseSampling('Decimated')).toBe('Decimated');
    expect(parseSampling('something-else')).toBe('Full');
    expect(parseSampling(null)).toBe('Full');
  });
});
