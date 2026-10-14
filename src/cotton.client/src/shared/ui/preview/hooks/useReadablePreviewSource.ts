import { useEffect, useMemo, useState } from "react";
import type { NodeFileManifestDto } from "../../../api/nodesApi";
import {
  getReadableFileUrl,
  type ReadableFileHandle,
} from "../../../crypto/downloadDecrypt";
import { useVault } from "../../../crypto/vault";
import { reportClientError } from "../../../utils/clientDiagnostics";

interface ReadablePreviewState {
  source: symbol;
  url: string | null;
  failed: boolean;
}

export function useReadablePreviewSource(file: NodeFileManifestDto | null) {
  const masterKey = useVault((state) => state.masterKey);
  const source = useMemo(
    () => (file && masterKey ? Symbol() : null),
    [file, masterKey],
  );
  const [state, setState] = useState<ReadablePreviewState | null>(null);

  useEffect(() => {
    if (!file || !masterKey || source === null) {
      return;
    }
    let active = true;
    let readable: ReadableFileHandle | null = null;

    void getReadableFileUrl(file).then(
      (handle) => {
        if (!active || useVault.getState().masterKey !== masterKey) {
          handle.revoke();
          return;
        }
        readable = handle;
        setState({ source, url: handle.url, failed: false });
      },
      (error: Error) => {
        if (active) {
          reportClientError("Failed to load encrypted preview", error);
          setState({ source, url: null, failed: true });
        }
      },
    );

    return () => {
      active = false;
      readable?.revoke();
    };
  }, [file, masterKey, source]);

  const current = state !== null && state.source === source;
  return {
    url: current ? state.url : null,
    failed: !masterKey || (current && state.failed),
    onSourceError: () => {
      if (source !== null) {
        setState({ source, url: null, failed: true });
      }
    },
  };
}
