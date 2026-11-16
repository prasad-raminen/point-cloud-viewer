/**
 * Height colour ramp (blue -> cyan -> green -> yellow -> red), the conventional elevation
 * palette for LiDAR viewers. Stops are evenly spaced and interpolated linearly in RGB.
 */

export type Rgb = [number, number, number];

export const HEIGHT_RAMP: readonly Rgb[] = [
  [0.19, 0.21, 0.58],
  [0.12, 0.57, 0.83],
  [0.16, 0.73, 0.45],
  [0.98, 0.84, 0.25],
  [0.86, 0.2, 0.16],
];

/** Clamps `t` into [0, 1]; NaN maps to 0. */
export function clamp01(t: number): number {
  if (Number.isNaN(t)) return 0;
  return Math.min(1, Math.max(0, t));
}

/** Samples a ramp at `t` in [0, 1] (values outside are clamped). */
export function sampleRamp(t: number, ramp: readonly Rgb[] = HEIGHT_RAMP): Rgb {
  if (ramp.length === 0) throw new Error('Ramp must contain at least one colour.');
  const first = ramp[0] as Rgb;
  if (ramp.length === 1) return [...first];

  const scaled = clamp01(t) * (ramp.length - 1);
  const index = Math.min(Math.floor(scaled), ramp.length - 2);
  const local = scaled - index;
  const a = ramp[index] as Rgb;
  const b = ramp[index + 1] as Rgb;
  // a*(1-t) + b*t (rather than a + (b-a)*t) reproduces both end stops exactly.
  const mix = (i: 0 | 1 | 2) => a[i] * (1 - local) + b[i] * local;
  return [mix(0), mix(1), mix(2)];
}

/**
 * Builds a per-point colour array from z values in `positions` (packed xyz).
 * A degenerate (flat) range colours everything with the ramp's midpoint.
 */
export function heightColors(
  positions: Float32Array,
  zRange: readonly [number, number],
  ramp: readonly Rgb[] = HEIGHT_RAMP,
): Float32Array {
  const count = Math.floor(positions.length / 3);
  const out = new Float32Array(count * 3);
  const [zMin, zMax] = zRange;
  const span = zMax - zMin;

  for (let i = 0; i < count; i++) {
    const z = positions[i * 3 + 2] as number;
    const t = span > 0 ? (z - zMin) / span : 0.5;
    const [r, g, b] = sampleRamp(t, ramp);
    out[i * 3] = r;
    out[i * 3 + 1] = g;
    out[i * 3 + 2] = b;
  }
  return out;
}

/** CSS linear-gradient for drawing the ramp as a legend. */
export function rampGradientCss(ramp: readonly Rgb[] = HEIGHT_RAMP): string {
  const stops = ramp.map(([r, g, b], i) => {
    const pct = ramp.length === 1 ? 0 : (i / (ramp.length - 1)) * 100;
    return `rgb(${Math.round(r * 255)} ${Math.round(g * 255)} ${Math.round(b * 255)}) ${pct.toFixed(0)}%`;
  });
  return `linear-gradient(to right, ${stops.join(', ')})`;
}
