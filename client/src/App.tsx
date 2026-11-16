import { useCallback, useEffect, useMemo, useState } from 'react';
import { deleteCloud, uploadCloud, type PointCloudDto } from './api/client';
import { Sidebar } from './components/Sidebar';
import { StatsPanel } from './components/StatsPanel';
import { Toolbar, type ColorMode } from './components/Toolbar';
import { Viewer } from './components/Viewer';
import { useDatasets } from './hooks/useDatasets';
import { usePoints } from './hooks/usePoints';
import { heightColors } from './lib/colorRamp';

export default function App() {
  const { datasets, loading: listLoading, error: listError, refresh } = useDatasets();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [colorMode, setColorMode] = useState<ColorMode>('rgb');
  const [pointSize, setPointSize] = useState(2);
  const [maxPoints, setMaxPoints] = useState(1_000_000);
  const [fitKey, setFitKey] = useState(0);
  const [actionError, setActionError] = useState<string | null>(null);
  const [dismissedError, setDismissedError] = useState<string | null>(null);

  const selected = datasets.find((d) => d.id === selectedId) ?? null;
  const { points, loading: pointsLoading, error: pointsError } = usePoints(selected?.id ?? null, maxPoints);

  // Select the first dataset once the list arrives (or when the selection disappears).
  useEffect(() => {
    if (!selected && datasets.length > 0) setSelectedId(datasets[0]!.id);
  }, [datasets, selected]);

  // Datasets without RGB can only be shown with the height ramp.
  useEffect(() => {
    if (selected && !selected.hasColor) setColorMode('height');
  }, [selected]);

  const activeColors = useMemo(() => {
    if (!points) return null;
    return colorMode === 'height' || !selected?.hasColor ? heightColors(points.positions, points.zRange) : points.colors;
  }, [points, colorMode, selected?.hasColor]);

  const fit = useCallback(() => setFitKey((k) => k + 1), []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      if (target && ['INPUT', 'SELECT', 'TEXTAREA'].includes(target.tagName)) return;
      if (e.key === 'f' || e.key === 'F') fit();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [fit]);

  const handleUpload = async (file: File) => {
    setActionError(null);
    try {
      const created = await uploadCloud(file);
      await refresh();
      setSelectedId(created.id);
      if (created.hasColor) setColorMode('rgb');
    } catch (e) {
      setActionError(`Upload failed: ${(e as Error).message}`);
    }
  };

  const handleDelete = async (dataset: PointCloudDto) => {
    if (!window.confirm(`Delete "${dataset.name}"? This cannot be undone.`)) return;
    setActionError(null);
    try {
      await deleteCloud(dataset.id);
      if (dataset.id === selectedId) setSelectedId(null);
      await refresh();
    } catch (e) {
      setActionError(`Delete failed: ${(e as Error).message}`);
    }
  };

  const currentError = actionError ?? pointsError ?? listError;
  const error = currentError !== dismissedError ? currentError : null;
  const dismissError = () => {
    setDismissedError(currentError);
    setActionError(null);
  };

  return (
    <div className="app">
      <Sidebar
        datasets={datasets}
        selectedId={selectedId}
        loading={listLoading}
        onSelect={setSelectedId}
        onUpload={handleUpload}
        onDelete={handleDelete}
      />

      <main className="stage">
        <Viewer positions={points?.positions ?? null} colors={activeColors} pointSize={pointSize} fitKey={fitKey} />

        {selected && (
          <Toolbar
            colorMode={colorMode}
            hasColor={selected.hasColor}
            pointSize={pointSize}
            maxPoints={maxPoints}
            onColorModeChange={setColorMode}
            onPointSizeChange={setPointSize}
            onMaxPointsChange={setMaxPoints}
            onFit={fit}
          />
        )}

        {selected && <StatsPanel dataset={selected} points={points} />}

        {pointsLoading && <div className="loading" role="status">Loading points…</div>}
        {!selected && !listLoading && <div className="empty">Select or upload a point cloud</div>}
        {error && (
          <div className="toast" role="alert">
            <span>{error}</span>
            <button className="icon-btn" aria-label="Dismiss" onClick={dismissError}>
              ×
            </button>
          </div>
        )}
      </main>
    </div>
  );
}
