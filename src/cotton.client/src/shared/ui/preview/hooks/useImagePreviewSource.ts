import { useEffect, useMemo, useState } from "react";
import type { NodeFileManifestDto } from "../../../api/nodesApi";
import {
  getReadableFileUrl,
  type ReadableFileHandle,
} from "../../../crypto/downloadDecrypt";
import { useVault } from "../../../crypto/vault";
import { reportClientError } from "../../../utils/clientDiagnostics";

interface ImagePreviewState {
  source: symbol;
  url: string | null;
  failed: boolean;
}

export function useImagePreviewSource(file: NodeFileManifestDto | null) {
  const masterKey = useVault((state) => state.masterKey);
  const source = useMemo(() => Symbol(), [file, masterKey]);
  const [state, setState] = useState<ImagePreviewState | null>(null);

  useEffect(() => {
    if (!file || !masterKey) {
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
          reportClientError("Failed to load encrypted image", error);
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
    onImageError: () => {
      if (file && masterKey) {
        setState({ source, url: null, failed: true });
      }
    },
  };
}
