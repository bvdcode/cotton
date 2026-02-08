import { useState, useRef, useCallback } from "react";

const DEFAULT_DURATION_MS = 2000;

/**
 * Provides a transient "copied" state for a button.
 * Returns `[isCopied, markCopied]` — call `markCopied()` after a successful
 * clipboard write and `isCopied` will be `true` for `durationMs`.
 */
export const useCopyFeedback = (
  durationMs: number = DEFAULT_DURATION_MS,
): [boolean, () => void] => {
  const [isCopied, setIsCopied] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const markCopied = useCallback(() => {
    setIsCopied(true);

    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }

    timerRef.current = setTimeout(() => {
      setIsCopied(false);
      timerRef.current = null;
    }, durationMs);
  }, [durationMs]);

  return [isCopied, markCopied];
};
