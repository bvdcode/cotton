import { useCallback, useEffect, useRef, useState } from "react";
import { SAVED_STATUS_VISIBLE_MS } from "./adminSettingSaveStatus";
import type { SaveStatus } from "./useAutoSavedSetting";

export const useSaveStatus = (initial: SaveStatus = "loading") => {
  const [status, setStatus] = useState<SaveStatus>(initial);
  const flashTimerRef = useRef<number | null>(null);

  useEffect(
    () => () => {
      if (flashTimerRef.current !== null) {
        window.clearTimeout(flashTimerRef.current);
      }
    },
    [],
  );

  const markSaved = useCallback(() => {
    if (flashTimerRef.current !== null) {
      window.clearTimeout(flashTimerRef.current);
    }

    setStatus("saved");
    flashTimerRef.current = window.setTimeout(() => {
      setStatus((current) => (current === "saved" ? "idle" : current));
      flashTimerRef.current = null;
    }, SAVED_STATUS_VISIBLE_MS);
  }, []);

  return { markSaved, setStatus, status };
};
