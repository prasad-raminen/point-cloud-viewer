import { useEffect, useState } from 'react';
import { fetchPoints } from '../api/client';
import type { DecodedPoints } from '../lib/pointBuffer';

/** Fetches and decodes the points of the selected dataset, cancelling stale requests. */
export function usePoints(id: string | null, maxPoints: number) {
  const [points, setPoints] = useState<DecodedPoints | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      setPoints(null);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);
    fetchPoints(id, maxPoints, controller.signal)
      .then(setPoints)
      .catch((e: Error) => {
        if (e.name !== 'AbortError') {
          setError(e.message);
          setPoints(null);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [id, maxPoints]);

  return { points, loading, error };
}
