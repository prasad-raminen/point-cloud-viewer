import { describe, expect, it } from 'vitest';
import { boundingSphere, fitDistance, overviewPosition } from './camera';

describe('fitDistance', () => {
  it('places the sphere exactly in view (before margin) for a square viewport', () => {
    // 90 degree FOV: half-angle 45 deg, so distance = r / sin(45 deg).
    expect(fitDistance(10, 90, 1, 1)).toBeCloseTo(10 / Math.sin(Math.PI / 4));
  });

  it('backs off further for narrow (portrait) viewports', () => {
    expect(fitDistance(10, 50, 0.5)).toBeGreaterThan(fitDistance(10, 50, 1));
  });

  it('returns a safe default for empty or invalid radii', () => {
    expect(fitDistance(0, 50)).toBe(1);
    expect(fitDistance(Number.NaN, 50)).toBe(1);
  });
});

describe('overviewPosition', () => {
  it('stays at the requested distance from the centre and above it', () => {
    const center: [number, number, number] = [5, -5, 2];
    const [x, y, z] = overviewPosition(center, 100);

    expect(Math.hypot(x - 5, y + 5, z - 2)).toBeCloseTo(100);
    expect(z).toBeGreaterThan(2);
    expect(x).toBeLessThan(5); // looking from the south-west
    expect(y).toBeLessThan(-5);
  });
});

describe('boundingSphere', () => {
  it('centres on the box centre and reaches the farthest point', () => {
    const sphere = boundingSphere(new Float32Array([-1, -2, 0, 3, 2, 4]));

    expect(sphere.center).toEqual([1, 0, 2]);
    expect(sphere.radius).toBeCloseTo(Math.sqrt(4 + 4 + 4));
  });

  it('handles empty input', () => {
    expect(boundingSphere(new Float32Array())).toEqual({ center: [0, 0, 0], radius: 0 });
  });
});
