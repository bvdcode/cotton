import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "@shared/ui/notifications";
import { showApiErrorToast } from "../../../shared/api/httpClient";
import { SAVED_STATUS_VISIBLE_MS } from "./adminSettingSaveStatus";

export type SaveStatus = "loading" | "idle" | "saving" | "saved" | "error";

interface UseAutoSavedSettingArgs<T> {
  initial: T;
  load: () => Promise<T>;
  save: (value: T) => Promise<void>;
  toastIdPrefix: string;
  loadErrorMessage: string;
  saveErrorMessage: string;
  isEqual?: (a: T, b: T) => boolean;
}

export interface UseAutoSavedSettingResult<T> {
  value: T;
  savedValue: T;
  setValue: (value: T) => void;
  commit: () => void;
  commitValue: (value: T) => void;
  status: SaveStatus;
  loadFailed: boolean;
}

export const useAutoSavedSetting = <T>({
  initial,
  load,
  save,
  toastIdPrefix,
  loadErrorMessage,
  saveErrorMessage,
  isEqual = Object.is,
}: UseAutoSavedSettingArgs<T>): UseAutoSavedSettingResult<T> => {
  const [value, setValueState] = useState<T>(initial);
  const [savedValue, setSavedValue] = useState<T>(initial);
  const [status, setStatus] = useState<SaveStatus>("loading");
  const [loadFailed, setLoadFailed] = useState(false);

  const loadRef = useRef(load);
  const saveRef = useRef(save);
  const isEqualRef = useRef(isEqual);
  const loadErrorMessageRef = useRef(loadErrorMessage);
  const saveErrorMessageRef = useRef(saveErrorMessage);
  const toastIdPrefixRef = useRef(toastIdPrefix);
  const savedValueRef = useRef(savedValue);
  const loadFailedRef = useRef(false);
  const flashTimerRef = useRef<number | null>(null);

  useEffect(() => {
    loadRef.current = load;
    saveRef.current = save;
    isEqualRef.current = isEqual;
    loadErrorMessageRef.current = loadErrorMessage;
    saveErrorMessageRef.current = saveErrorMessage;
    toastIdPrefixRef.current = toastIdPrefix;
    savedValueRef.current = savedValue;
  });

  useEffect(() => {
    let active = true;

    loadRef
      .current()
      .then((loaded) => {
        if (!active) return;
        setValueState(loaded);
        setSavedValue(loaded);
        loadFailedRef.current = false;
        setLoadFailed(false);
        setStatus("idle");
      })
      .catch(() => {
        if (!active) return;
        loadFailedRef.current = true;
        setStatus("error");
        setLoadFailed(true);
        toast.error(loadErrorMessageRef.current, {
          toastId: `${toastIdPrefixRef.current}:load-error`,
        });
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(
    () => () => {
      if (flashTimerRef.current !== null) {
        window.clearTimeout(flashTimerRef.current);
      }
    },
    [],
  );

  const persist = useCallback(async (next: T) => {
    if (loadFailedRef.current) {
      return;
    }

    if (flashTimerRef.current !== null) {
      window.clearTimeout(flashTimerRef.current);
      flashTimerRef.current = null;
    }

    setStatus("saving");
    try {
      await saveRef.current(next);
      setSavedValue(next);
      setStatus("saved");
      flashTimerRef.current = window.setTimeout(() => {
        setStatus((current) => (current === "saved" ? "idle" : current));
        flashTimerRef.current = null;
      }, SAVED_STATUS_VISIBLE_MS);
    } catch (error) {
      setValueState(savedValueRef.current);
      setStatus("error");
      showApiErrorToast(
        error,
        saveErrorMessageRef.current,
        `${toastIdPrefixRef.current}:save-error`,
      );
    }
  }, []);

  const setValue = useCallback((next: T) => {
    if (loadFailedRef.current) {
      return;
    }

    setValueState(next);
  }, []);

  const commit = useCallback(() => {
    if (loadFailedRef.current) return;
    if (isEqualRef.current(value, savedValueRef.current)) return;
    void persist(value);
  }, [persist, value]);

  const commitValue = useCallback(
    (next: T) => {
      if (loadFailedRef.current) return;
      setValueState(next);
      if (isEqualRef.current(next, savedValueRef.current)) return;
      void persist(next);
    },
    [persist],
  );

  return {
    value,
    savedValue,
    setValue,
    commit,
    commitValue,
    status,
    loadFailed,
  };
};
