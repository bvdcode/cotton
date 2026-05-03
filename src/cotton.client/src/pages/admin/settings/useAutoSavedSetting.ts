import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "react-toastify";
import {
  hasApiErrorToastBeenDispatched,
  isAxiosError,
} from "../../../shared/api/httpClient";

export type SaveStatus = "loading" | "idle" | "saving" | "saved" | "error";

interface UseAutoSavedSettingArgs<T> {
  initial: T;
  load: () => Promise<T>;
  save: (value: T) => Promise<void>;
  toastIdPrefix: string;
  errorMessage: string;
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

const SAVED_FLASH_MS = 1500;

export const useAutoSavedSetting = <T,>({
  initial,
  load,
  save,
  toastIdPrefix,
  errorMessage,
  isEqual = Object.is,
}: UseAutoSavedSettingArgs<T>): UseAutoSavedSettingResult<T> => {
  const [value, setValueState] = useState<T>(initial);
  const [savedValue, setSavedValue] = useState<T>(initial);
  const [status, setStatus] = useState<SaveStatus>("loading");
  const [loadFailed, setLoadFailed] = useState(false);

  const loadRef = useRef(load);
  const saveRef = useRef(save);
  const isEqualRef = useRef(isEqual);
  const errorMessageRef = useRef(errorMessage);
  const toastIdPrefixRef = useRef(toastIdPrefix);
  const savedValueRef = useRef(savedValue);
  const flashTimerRef = useRef<number | null>(null);

  loadRef.current = load;
  saveRef.current = save;
  isEqualRef.current = isEqual;
  errorMessageRef.current = errorMessage;
  toastIdPrefixRef.current = toastIdPrefix;
  savedValueRef.current = savedValue;

  useEffect(() => {
    let active = true;

    setStatus("loading");
    setLoadFailed(false);

    loadRef
      .current()
      .then((loaded) => {
        if (!active) return;
        setValueState(loaded);
        setSavedValue(loaded);
        setStatus("idle");
      })
      .catch(() => {
        if (!active) return;
        setStatus("idle");
        setLoadFailed(true);
        toast.error(errorMessageRef.current, {
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
      }, SAVED_FLASH_MS);
    } catch (error) {
      setValueState(savedValueRef.current);
      setStatus("error");
      if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
        toast.error(errorMessageRef.current, {
          toastId: `${toastIdPrefixRef.current}:save-error`,
        });
      }
    }
  }, []);

  const setValue = useCallback((next: T) => {
    setValueState(next);
  }, []);

  const commit = useCallback(() => {
    if (isEqualRef.current(value, savedValueRef.current)) return;
    void persist(value);
  }, [persist, value]);

  const commitValue = useCallback(
    (next: T) => {
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
