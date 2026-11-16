import { describe, expect, it } from 'vitest';
import { HEIGHT_RAMP, clamp01, heightColors, rampGradientCss, sampleRamp, type Rgb } from './colorRamp';

const BLACK_TO_WHITE: Rgb[] = [
  [0, 0, 0],
  [1, 1, 1],
];

describe('sampleRamp', () => {
  it('returns the end stops at 0 and 1', () => {
    expect(sampleRamp(0)).toEqual(HEIGHT_RAMP[0]);
    expect(sampleRamp(1)).toEqual(HEIGHT_RAMP[HEIGHT_RAMP.length - 1]);
  });

  it('interpolates linearly between stops', () => {
    expect(sampleRamp(0.25, BLACK_TO_WHITE)).toEqual([0.25, 0.25, 0.25]);

    const [r, g, b] = sampleRamp(0.125); // halfway between stop 0 and stop 1 of a 5-stop ramp
    const [a0, a1] = [HEIGHT_RAMP[0]!, HEIGHT_RAMP[1]!];
    expect(r).toBeCloseTo((a0[0] + a1[0]) / 2);
    expect(g).toBeCloseTo((a0[1] + a1[1]) / 2);
    expect(b).toBeCloseTo((a0[2] + a1[2]) / 2);
  });

  it('clamps out-of-range and NaN inputs', () => {
    expect(sampleRamp(-3, BLACK_TO_WHITE)).toEqual([0, 0, 0]);
    expect(sampleRamp(7, BLACK_TO_WHITE)).toEqual([1, 1, 1]);
    expect(sampleRamp(Number.NaN, BLACK_TO_WHITE)).toEqual([0, 0, 0]);
  });

  it('handles single-stop ramps and rejects empty ones', () => {
    expect(sampleRamp(0.7, [[0.2, 0.4, 0.6]])).toEqual([0.2, 0.4, 0.6]);
    expect(() => sampleRamp(0.5, [])).toThrow();
  });
});

describe('heightColors', () => {
  it('maps z linearly onto the ramp across the given range', () => {
    const positions = new Float32Array([0, 0, 10, 0, 0, 15, 0, 0, 20]);

    const colors = heightColors(positions, [10, 20], BLACK_TO_WHITE);

    expect(Array.from(colors)).toEqual([0, 0, 0, 0.5, 0.5, 0.5, 1, 1, 1]);
  });

  it('uses the ramp midpoint for a flat cloud', () => {
    const colors = heightColors(new Float32Array([1, 2, 3, 4, 5, 3]), [3, 3], BLACK_TO_WHITE);

    expect(Array.from(colors)).toEqual([0.5, 0.5, 0.5, 0.5, 0.5, 0.5]);
  });

  it('returns three channels per point', () => {
    expect(heightColors(new Float32Array(30), [0, 1])).toHaveLength(30);
  });
});

describe('clamp01 and rampGradientCss', () => {
  it('clamps', () => {
    expect(clamp01(-1)).toBe(0);
    expect(clamp01(0.3)).toBe(0.3);
    expect(clamp01(2)).toBe(1);
  });

  it('renders a CSS gradient with evenly spaced stops', () => {
    expect(rampGradientCss(BLACK_TO_WHITE)).toBe('linear-gradient(to right, rgb(0 0 0) 0%, rgb(255 255 255) 100%)');
  });
});
