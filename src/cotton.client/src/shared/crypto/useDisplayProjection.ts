import { useEffect, useState } from "react";
import { useVault } from "./vault";
import { reportClientError } from "../utils/clientDiagnostics";

export function useDisplayProjection<T>(
  source: T,
  project: (value: T) => Promise<T>,
): T {
  const key = useVault((state) => state.masterKey);
  const [display, setDisplay] = useState<{
    source: T;
    key: CryptoKey;
    value: T;
  } | null>(null);
  useEffect(() => {
    const unsubscribe = useVault.subscribe((state) => {
      if (state.masterKey !== key) {
        setDisplay(null);
      }
    });
    if (!key) {
      return unsubscribe;
    }
    let cancelled = false;
    void project(source)
      .then((value) => {
        if (!cancelled && useVault.getState().masterKey === key) {
          setDisplay({ source, key, value });
        }
      })
      .catch((error: Error) =>
        reportClientError("Failed to project display metadata", error),
      );
    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, [source, key, project]);
  return display !== null && display.source === source && display.key === key
    ? display.value
    : source;
}
