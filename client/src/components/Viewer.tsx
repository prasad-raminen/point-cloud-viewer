import { useEffect, useLayoutEffect, useMemo } from 'react';
import { Canvas, useThree } from '@react-three/fiber';
import { GizmoHelper, GizmoViewport, OrbitControls } from '@react-three/drei';
import { BufferAttribute, BufferGeometry, PerspectiveCamera, type Vector3 } from 'three';
import { boundingSphere, fitDistance, overviewPosition, type Sphere } from '../lib/camera';

const FOV = 50;

/** The subset of OrbitControls this component relies on. */
interface TargetControls {
  target: Vector3;
  update: () => void;
}

interface ViewerProps {
  positions: Float32Array | null;
  colors: Float32Array | null;
  pointSize: number;
  /** Incremented to request re-framing the camera on the current points. */
  fitKey: number;
}

/** 3D canvas: renders points with a single BufferGeometry draw call and orbit navigation (Z-up). */
export function Viewer({ positions, colors, pointSize, fitKey }: ViewerProps) {
  const sphere = useMemo(() => (positions ? boundingSphere(positions) : null), [positions]);

  return (
    <Canvas
      className="viewer-canvas"
      dpr={[1, 2]}
      camera={{ fov: FOV, near: 0.1, far: 100_000, up: [0, 0, 1], position: [60, -60, 50] }}
      gl={{ antialias: false, powerPreference: 'high-performance' }}
    >
      <color attach="background" args={['#0b1020']} />
      {positions && colors && <PointCloud positions={positions} colors={colors} size={pointSize} />}
      <OrbitControls makeDefault enableDamping dampingFactor={0.12} screenSpacePanning />
      {sphere && <CameraFitter sphere={sphere} fitKey={fitKey} />}
      <GizmoHelper alignment="bottom-right" margin={[64, 64]}>
        <GizmoViewport axisColors={['#f87171', '#4ade80', '#60a5fa']} labelColor="#0b1020" />
      </GizmoHelper>
    </Canvas>
  );
}

function PointCloud({ positions, colors, size }: { positions: Float32Array; colors: Float32Array; size: number }) {
  const dpr = useThree((s) => s.viewport.dpr);

  const geometry = useMemo(() => {
    const g = new BufferGeometry();
    g.setAttribute('position', new BufferAttribute(positions, 3));
    g.computeBoundingSphere();
    return g;
  }, [positions]);

  // Swapping only the colour attribute keeps colour-mode changes cheap (positions stay on the GPU).
  // A layout effect so the attribute exists before the first frame is drawn.
  useLayoutEffect(() => {
    if (colors.length === positions.length) {
      geometry.setAttribute('color', new BufferAttribute(colors, 3));
    }
  }, [geometry, colors, positions.length]);

  useEffect(() => () => geometry.dispose(), [geometry]);

  return (
    <points geometry={geometry} frustumCulled={false}>
      <pointsMaterial vertexColors size={size * dpr} sizeAttenuation={false} />
    </points>
  );
}

/** Frames the camera on the cloud's bounding sphere whenever the sphere or `fitKey` changes. */
function CameraFitter({ sphere, fitKey }: { sphere: Sphere; fitKey: number }) {
  const camera = useThree((s) => s.camera);
  const controls = useThree((s) => s.controls) as unknown as TargetControls | null;
  const aspect = useThree((s) => s.size.width / Math.max(1, s.size.height));

  useEffect(() => {
    if (!(camera instanceof PerspectiveCamera)) return;
    const distance = fitDistance(sphere.radius, camera.fov, aspect);
    const [x, y, z] = overviewPosition(sphere.center, distance);
    camera.position.set(x, y, z);
    camera.near = Math.max(distance / 2000, 0.001);
    camera.far = distance * 50;
    camera.updateProjectionMatrix();
    if (controls) {
      controls.target.set(...sphere.center);
      controls.update();
    } else {
      camera.lookAt(...sphere.center);
    }
    // `aspect` is intentionally not a dependency: resizing the window should not re-frame the scene.
  }, [sphere, fitKey, camera, controls]);

  return null;
}
