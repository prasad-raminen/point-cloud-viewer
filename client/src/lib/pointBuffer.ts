/**
 * Decoding of the API's binary point format: interleaved little-endian Float32 records
 * of [x, y, z, r, g, b]. Positions are relative to `origin`; colours are 0..1.
 */

export type Vec3 = { x: number; y: number; z: number };

export type PointSampling = 'Full' | 'VoxelLod' | 'Decimated';

export interface DecodedPoints {
  /** Number of points in this buffer. */
  count: number;
  /** Total points in the dataset (may exceed `count` when the server sampled). */
  totalCount: number;
  /** Packed xyz positions, length = 3 * count. */
  positions: Float32Array;
  /** Packed rgb colours in 0..1, length = 3 * count. */
  colors: Float32Array;
  /** World-space offset that was subtracted from every position. */
  origin: Vec3;
  sampling: PointSampling;
  /** Local-space z range of the decoded points (useful for height colouring). */
  zRange: [number, number];
}

export const FLOATS_PER_POINT = 6;
export const BYTES_PER_POINT = FLOATS_PER_POINT * 4;

/** Parses the X-Point-Origin header ("x,y,z"). Returns the zero vector when absent or malformed. */
export function parseOrigin(header: string | null | undefined): Vec3 {
  const parts = (header ?? '').split(',').map((p) => Number.parseFloat(p.trim()));
  if (parts.length !== 3 || parts.some((n) => !Number.isFinite(n))) {
    return { x: 0, y: 0, z: 0 };
  }
  const [x, y, z] = parts as [number, number, number];
  return { x, y, z };
}

export function parseSampling(header: string | null | undefined): PointSampling {
  return header === 'VoxelLod' || header === 'Decimated' ? header : 'Full';
}

/**
 * Splits an interleaved buffer into separate position and colour arrays, as expected by
 * three.js BufferGeometry attributes. Uses a DataView so decoding is endian-correct on any host.
 */
export function decodePointBuffer(
  buffer: ArrayBuffer,
  meta: { origin?: Vec3; sampling?: PointSampling; totalCount?: number } = {},
): DecodedPoints {
  if (buffer.byteLength % BYTES_PER_POINT !== 0) {
    throw new Error(
      `Point buffer length ${buffer.byteLength} is not a multiple of ${BYTES_PER_POINT} bytes.`,
    );
  }

  const count = buffer.byteLength / BYTES_PER_POINT;
  const view = new DataView(buffer);
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);
  let zMin = Number.POSITIVE_INFINITY;
  let zMax = Number.NEGATIVE_INFINITY;

  for (let i = 0; i < count; i++) {
    const base = i * BYTES_PER_POINT;
    const z = view.getFloat32(base + 8, true);
    positions[i * 3] = view.getFloat32(base, true);
    positions[i * 3 + 1] = view.getFloat32(base + 4, true);
    positions[i * 3 + 2] = z;
    colors[i * 3] = view.getFloat32(base + 12, true);
    colors[i * 3 + 1] = view.getFloat32(base + 16, true);
    colors[i * 3 + 2] = view.getFloat32(base + 20, true);
    if (z < zMin) zMin = z;
    if (z > zMax) zMax = z;
  }

  return {
    count,
    totalCount: meta.totalCount ?? count,
    positions,
    colors,
    origin: meta.origin ?? { x: 0, y: 0, z: 0 },
    sampling: meta.sampling ?? 'Full',
    zRange: count === 0 ? [0, 0] : [zMin, zMax],
  };
}
