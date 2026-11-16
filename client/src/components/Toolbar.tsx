import { rampGradientCss } from '../lib/colorRamp';

export type ColorMode = 'rgb' | 'height';

export const POINT_BUDGETS = [250_000, 1_000_000, 2_000_000, 5_000_000] as const;

interface ToolbarProps {
  colorMode: ColorMode;
  hasColor: boolean;
  pointSize: number;
  maxPoints: number;
  onColorModeChange: (mode: ColorMode) => void;
  onPointSizeChange: (size: number) => void;
  onMaxPointsChange: (max: number) => void;
  onFit: () => void;
}

/** Floating display controls over the canvas. */
export function Toolbar(props: ToolbarProps) {
  const { colorMode, hasColor, pointSize, maxPoints } = props;

  return (
    <div className="toolbar" role="toolbar" aria-label="Display options">
      <div className="segmented" role="radiogroup" aria-label="Colour mode">
        <button
          role="radio"
          aria-checked={colorMode === 'rgb'}
          className={colorMode === 'rgb' ? 'active' : ''}
          disabled={!hasColor}
          title={hasColor ? 'Colour from scan RGB' : 'This dataset has no RGB values'}
          onClick={() => props.onColorModeChange('rgb')}
        >
          RGB
        </button>
        <button
          role="radio"
          aria-checked={colorMode === 'height'}
          className={colorMode === 'height' ? 'active' : ''}
          onClick={() => props.onColorModeChange('height')}
        >
          Height
        </button>
      </div>

      {colorMode === 'height' && (
        <span className="legend" style={{ background: rampGradientCss() }} title="Low → high" aria-hidden="true" />
      )}

      <label className="control">
        <span>Point size</span>
        <input
          type="range"
          min={1}
          max={8}
          step={0.5}
          value={pointSize}
          onChange={(e) => props.onPointSizeChange(Number(e.target.value))}
        />
        <output>{pointSize}px</output>
      </label>

      <label className="control">
        <span>Budget</span>
        <select value={maxPoints} onChange={(e) => props.onMaxPointsChange(Number(e.target.value))}>
          {POINT_BUDGETS.map((n) => (
            <option key={n} value={n}>
              {n >= 1_000_000 ? `${n / 1_000_000}M` : `${n / 1_000}k`} pts
            </option>
          ))}
        </select>
      </label>

      <button className="btn" onClick={props.onFit} title="Fit to view (F)">
        Fit view
      </button>
    </div>
  );
}
