import { useEffect, useState } from "react";
import type { NodeContentDto } from "../api/nodesApi";
import { applyDisplayMetaToFiles } from "./displayMeta";
import { useVault } from "./vault";

export function useDecryptedFolderContent(source: NodeContentDto | undefined) {
  const key = useVault((state) => state.masterKey);
  const [display, setDisplay] = useState<{
    source: NodeContentDto;
    key: CryptoKey;
    content: NodeContentDto;
  } | null>(null);

  useEffect(() => {
    if (!source || !key) {
      return;
    }
    let cancelled = false;
    void applyDisplayMetaToFiles(source.files).then((files) => {
      if (!cancelled) {
        setDisplay({ source, key, content: { ...source, files } });
      }
    });
    return () => {
      cancelled = true;
    };
  }, [source, key]);

  return display?.source === source && display?.key === key
    ? display.content
    : source;
}
