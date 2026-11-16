import { useCallback, useEffect, useState } from 'react';
import { listClouds, type PointCloudDto } from '../api/client';

/** Loads the dataset list; re-polls briefly while the server may still be seeding demo data. */
export function useDatasets() {
  const [datasets, setDatasets] = useState<PointCloudDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async (signal?: AbortSignal) => {
    try {
      const list = await listClouds(signal);
      setDatasets(list);
      setError(null);
      return list;
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError((e as Error).message);
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let attempts = 0;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const poll = async () => {
      const list = await refresh(controller.signal);
      // Demo data is generated in the background on first start; retry a few times if empty.
      if (list && list.length === 0 && attempts++ < 5 && !controller.signal.aborted) {
        timer = setTimeout(poll, 1500);
      }
    };
    void poll();

    return () => {
      controller.abort();
      if (timer) clearTimeout(timer);
    };
  }, [refresh]);

  return { datasets, loading, error, refresh };
}
