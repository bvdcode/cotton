import { useCallback, useEffect, useRef, useState } from "react";
import type { MouseEvent, PointerEvent } from "react";

const HOLD_DELAY_MS = 500;
const MOVE_TOLERANCE_PX = 8;

export const useLongPress = (onLongPress: () => void, disabled = false) => {
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const start = useRef<{ id: number; x: number; y: number } | null>(null);
  const held = useRef(false);
  const [ready, setReady] = useState(false);

  const clear = useCallback(() => {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
    start.current = null;
  }, []);

  const cancel = useCallback(() => {
    clear();
    setReady(false);
  }, [clear]);

  if (disabled && ready) {
    setReady(false);
  }

  useEffect(() => {
    if (disabled) {
      clear();
    }
    return clear;
  }, [clear, disabled]);

  const handlers = {
    onPointerDown: (event: PointerEvent<HTMLButtonElement>) => {
      cancel();
      held.current = false;
      if (disabled || event.button !== 0 || !event.isPrimary) {
        return;
      }
      start.current = {
        id: event.pointerId,
        x: event.clientX,
        y: event.clientY,
      };
      timer.current = setTimeout(() => {
        held.current = true;
        timer.current = null;
        setReady(true);
      }, HOLD_DELAY_MS);
    },
    onPointerMove: (event: PointerEvent<HTMLButtonElement>) => {
      const origin = start.current;
      if (
        origin &&
        origin.id === event.pointerId &&
        (Math.abs(event.clientX - origin.x) > MOVE_TOLERANCE_PX ||
          Math.abs(event.clientY - origin.y) > MOVE_TOLERANCE_PX)
      ) {
        cancel();
      }
    },
    onPointerUp: (event: PointerEvent<HTMLButtonElement>) => {
      const activate =
        !disabled && held.current && start.current?.id === event.pointerId;
      cancel();
      if (activate) {
        onLongPress();
      }
    },
    onPointerCancel: cancel,
    onPointerLeave: cancel,
    onClickCapture: (event: MouseEvent<HTMLButtonElement>) => {
      if (held.current && event.detail !== 0) {
        held.current = false;
        event.preventDefault();
        event.stopPropagation();
      }
    },
    onContextMenu: (event: MouseEvent<HTMLButtonElement>) => {
      event.preventDefault();
    },
  };
  return { handlers, ready };
};
