/** Camera framing helpers (pure maths so they can be unit tested without WebGL). */

export type Tuple3 = [number, number, number];

/**
 * Distance from a sphere's centre at which a perspective camera with vertical field of view
 * `fovDeg` (and the given aspect ratio) sees the whole sphere, plus a margin.
 */
export function fitDistance(radius: number, fovDeg: number, aspect = 1, margin = 1.15): number {
  if (!(radius > 0)) return 1;
  const vFov = (fovDeg * Math.PI) / 180;
  const hFov = 2 * Math.atan(Math.tan(vFov / 2) * aspect);
  const limiting = Math.min(vFov, hFov);
  return (radius / Math.sin(limiting / 2)) * margin;
}

/**
 * Camera position for a "three-quarter" overview of a Z-up scene: looking down at ~35 degrees
 * from the south-west, which reads well for terrain and buildings.
 */
export function overviewPosition(center: Tuple3, distance: number): Tuple3 {
  const elevation = (35 * Math.PI) / 180;
  const azimuth = (-135 * Math.PI) / 180;
  const horizontal = Math.cos(elevation) * distance;
  return [
    center[0] + Math.cos(azimuth) * horizontal,
    center[1] + Math.sin(azimuth) * horizontal,
    center[2] + Math.sin(elevation) * distance,
  ];
}

export interface Sphere {
  center: Tuple3;
  radius: number;
}

/**
 * Bounding sphere of packed xyz positions: centred on the axis-aligned box centre, with the
 * radius reaching the farthest point. Slightly larger than optimal but cheap and stable.
 */
export function boundingSphere(positions: Float32Array): Sphere {
  const count = Math.floor(positions.length / 3);
  if (count === 0) return { center: [0, 0, 0], radius: 0 };

  const min: Tuple3 = [Infinity, Infinity, Infinity];
  const max: Tuple3 = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < count; i++) {
    for (let axis = 0; axis < 3; axis++) {
      const v = positions[i * 3 + axis] as number;
      if (v < min[axis]!) min[axis] = v;
      if (v > max[axis]!) max[axis] = v;
    }
  }

  const center: Tuple3 = [(min[0] + max[0]) / 2, (min[1] + max[1]) / 2, (min[2] + max[2]) / 2];
  let radiusSq = 0;
  for (let i = 0; i < count; i++) {
    const dx = (positions[i * 3] as number) - center[0];
    const dy = (positions[i * 3 + 1] as number) - center[1];
    const dz = (positions[i * 3 + 2] as number) - center[2];
    radiusSq = Math.max(radiusSq, dx * dx + dy * dy + dz * dz);
  }
  return { center, radius: Math.sqrt(radiusSq) };
}
