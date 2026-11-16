import { describe, expect, it } from 'vitest';
import { formatBytes, formatCompact, formatCount, formatLength } from './format';

describe('format helpers', () => {
  it('formats counts with grouping', () => {
    expect(formatCount(1234567)).toBe('1,234,567');
  });

  it('formats compact counts', () => {
    expect(formatCompact(950)).toBe('950');
    expect(formatCompact(1_500)).toBe('1.5k');
    expect(formatCompact(250_000)).toBe('250k');
    expect(formatCompact(2_000_000)).toBe('2M');
  });

  it('formats byte sizes', () => {
    expect(formatBytes(0)).toBe('—');
    expect(formatBytes(512)).toBe('512 B');
    expect(formatBytes(1536)).toBe('1.5 KB');
    expect(formatBytes(5 * 1024 * 1024)).toBe('5.0 MB');
  });

  it('chooses decimals by magnitude', () => {
    expect(formatLength(1.23456)).toBe('1.235');
    expect(formatLength(12.3456)).toBe('12.35');
    expect(formatLength(512345.678)).toBe('512345.7');
  });
});
