import { useRef, useState, type ChangeEvent } from 'react';
import type { PointCloudDto } from '../api/client';
import { formatCompact } from '../lib/format';

export const ACCEPTED_EXTENSIONS = '.xyz,.txt,.csv,.pts,.ply';

interface SidebarProps {
  datasets: PointCloudDto[];
  selectedId: string | null;
  loading: boolean;
  onSelect: (id: string) => void;
  onUpload: (file: File) => Promise<void>;
  onDelete: (dataset: PointCloudDto) => Promise<void>;
}

/** Dataset list with upload and delete actions. */
export function Sidebar({ datasets, selectedId, loading, onSelect, onUpload, onDelete }: SidebarProps) {
  const input = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);

  const handleFile = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setUploading(true);
    try {
      await onUpload(file);
    } finally {
      setUploading(false);
    }
  };

  return (
    <aside className="sidebar">
      <header className="brand">
        <span className="brand-mark" aria-hidden="true">
          <i />
          <i />
          <i />
        </span>
        <div>
          <h1>Point Cloud Viewer</h1>
          <p>ASCII XYZ &amp; PLY scans</p>
        </div>
      </header>

      <button className="btn btn-primary upload" onClick={() => input.current?.click()} disabled={uploading}>
        {uploading ? 'Processing…' : 'Upload point cloud'}
      </button>
      <input ref={input} type="file" accept={ACCEPTED_EXTENSIONS} hidden onChange={handleFile} />
      <p className="hint">.xyz / .txt / .csv / .pts (x y z [r g b]) or ASCII .ply</p>

      <h2 className="section-title">Datasets</h2>
      {loading && datasets.length === 0 && <p className="muted">Loading…</p>}
      {!loading && datasets.length === 0 && <p className="muted">No datasets yet. Upload a file to get started.</p>}

      <ul className="dataset-list">
        {datasets.map((d) => (
          <li key={d.id}>
            <button
              className={`dataset ${d.id === selectedId ? 'selected' : ''}`}
              onClick={() => onSelect(d.id)}
              aria-current={d.id === selectedId}
            >
              <span className="dataset-name" title={d.name}>
                {d.name}
              </span>
              <span className="dataset-meta">
                <span className="badge">{d.format}</span>
                {d.isSample && <span className="badge badge-sample">demo</span>}
                <span>{formatCompact(d.pointCount)} pts</span>
              </span>
            </button>
            <button
              className="icon-btn"
              title={`Delete ${d.name}`}
              aria-label={`Delete ${d.name}`}
              onClick={() => void onDelete(d)}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
    </aside>
  );
}
