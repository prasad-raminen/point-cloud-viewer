/** Display formatting helpers. */

const integer = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

export function formatCount(n: number): string {
  return integer.format(n);
}

/** Compact counts for tight UI (e.g. 1.2M, 950k). */
export function formatCompact(n: number): string {
  if (Math.abs(n) >= 1_000_000) return `${trim(n / 1_000_000)}M`;
  if (Math.abs(n) >= 1_000) return `${trim(n / 1_000)}k`;
  return String(n);
}

export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '—';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${unit === 0 ? value : value.toFixed(1)} ${units[unit]}`;
}

/** Formats a length with a sensible number of decimals for metres. */
export function formatLength(value: number): string {
  const abs = Math.abs(value);
  const digits = abs >= 1000 ? 1 : abs >= 10 ? 2 : 3;
  return value.toFixed(digits);
}

function trim(value: number): string {
  return value.toFixed(value >= 100 ? 0 : 1).replace(/\.0$/, '');
}
