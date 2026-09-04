import { useCallback, useEffect, useRef, useState, type DependencyList } from 'react';

/** State returned by useAsync / usePolling. `data` keeps the last successful value while reloading. */
export interface AsyncState<T> {
  data: T | null;
  error: Error | null;
  /** true only until the first result (or failure) of the current dependency set arrives */
  loading: boolean;
  /** epoch millis of the last successful load */
  updatedAt: number | null;
  /** re-runs the loader; the stale data stays visible until the new result arrives */
  reload: () => void;
}

/**
 * Runs `loader` on mount and whenever `deps` change; with `intervalMs` it also re-runs on a timer
 * (used for the auto-refreshing dashboard and system pages). Results of a superseded run (deps
 * changed or component unmounted) are discarded so they never overwrite newer data.
 */
export function useAsync<T>(loader: () => Promise<T>, deps: DependencyList, intervalMs?: number): AsyncState<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [loading, setLoading] = useState(true);
  const [updatedAt, setUpdatedAt] = useState<number | null>(null);
  const [tick, setTick] = useState(0);
  const loaderRef = useRef(loader);
  loaderRef.current = loader;
  const reload = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    const run = async () => {
      try {
        const result = await loaderRef.current();
        if (!cancelled) {
          setData(result);
          setError(null);
          setUpdatedAt(Date.now());
        }
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e : new Error(String(e)));
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    setLoading(true);
    void run();
    const timer = intervalMs && intervalMs > 0 ? window.setInterval(() => void run(), intervalMs) : undefined;
    return () => {
      cancelled = true;
      if (timer !== undefined) window.clearInterval(timer);
    };
    // deps are spread on purpose: the caller decides what triggers a reload.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick, intervalMs]);

  return { data, error, loading, updatedAt, reload };
}

/** useAsync with a refresh interval; `deps` are optional. */
export function usePolling<T>(loader: () => Promise<T>, intervalMs: number, deps: DependencyList = []): AsyncState<T> {
  return useAsync(loader, deps, intervalMs);
}
