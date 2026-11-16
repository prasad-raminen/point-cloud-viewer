import type { PointCloudDto } from '../api/client';
import type { DecodedPoints, Vec3 } from '../lib/pointBuffer';
import { formatBytes, formatCount, formatLength } from '../lib/format';

const SAMPLING_LABEL = {
  Full: 'full resolution',
  VoxelLod: 'voxel LOD',
  Decimated: 'decimated LOD',
} as const;

/** Dataset statistics: counts, bounds, centroid and import diagnostics. */
export function StatsPanel({ dataset, points }: { dataset: PointCloudDto; points: DecodedPoints | null }) {
  const { bounds } = dataset;

  return (
    <section className="stats" aria-label="Dataset statistics">
      <h2 title={dataset.name}>{dataset.name}</h2>
      <dl>
        <dt>Points</dt>
        <dd>{formatCount(dataset.pointCount)}</dd>

        <dt>Displayed</dt>
        <dd>
          {points ? (
            <>
              {formatCount(points.count)} <span className="muted">({SAMPLING_LABEL[points.sampling]})</span>
            </>
          ) : (
            '—'
          )}
        </dd>

        {dataset.lodVoxelSize !== null && (
          <>
            <dt>LOD</dt>
            <dd>
              {formatCount(dataset.lodPointCount)} pts @ {formatLength(dataset.lodVoxelSize)} voxel
            </dd>
          </>
        )}

        <dt>Min</dt>
        <dd className="mono">{vec(bounds.min)}</dd>
        <dt>Max</dt>
        <dd className="mono">{vec(bounds.max)}</dd>
        <dt>Size</dt>
        <dd className="mono">{vec(bounds.size)}</dd>
        <dt>Centroid</dt>
        <dd className="mono">{vec(dataset.centroid)}</dd>

        <dt>Source</dt>
        <dd title={dataset.sourceFileName}>
          {dataset.format.toUpperCase()} · {formatBytes(dataset.sourceSizeBytes)}
        </dd>
        <dt>Colour</dt>
        <dd>{dataset.hasColor ? 'RGB' : 'none'}</dd>
      </dl>

      {dataset.skippedLines > 0 && (
        <details className="warnings">
          <summary>
            {formatCount(dataset.skippedLines)} malformed line{dataset.skippedLines === 1 ? '' : 's'} skipped
          </summary>
          <ul>
            {dataset.warnings.map((w) => (
              <li key={w}>{w}</li>
            ))}
          </ul>
        </details>
      )}
    </section>
  );
}

function vec(v: Vec3): string {
  return `${formatLength(v.x)}, ${formatLength(v.y)}, ${formatLength(v.z)}`;
}
