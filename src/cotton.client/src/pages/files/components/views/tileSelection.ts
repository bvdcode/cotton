import React from "react";

export type TileLongPressHandlers = Pick<
  React.HTMLAttributes<HTMLDivElement>,
  | "onClickCapture"
  | "onPointerCancelCapture"
  | "onPointerDownCapture"
  | "onPointerMoveCapture"
  | "onPointerUpCapture"
>;
const ignoredLongPressSelector =
  ".card-menu-slot, .card-menu-button, button, a, input, textarea, [role='menuitem']";

const shouldIgnoreLongPressTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof Element)) return false;
  return Boolean(target.closest(ignoredLongPressSelector));
};

export const useLongPressSelection = (options: {
  onToggle?: (shiftKey: boolean) => void;
  readOnly: boolean;
  selectionMode: boolean;
}): TileLongPressHandlers => {
  const { onToggle, readOnly, selectionMode } = options;
  const longPressTimerRef = React.useRef<number | null>(null);
  const suppressClickUntilRef = React.useRef(0);
  const longPressStartRef = React.useRef<{ x: number; y: number } | null>(null);

  const clearLongPress = React.useCallback(() => {
    if (longPressTimerRef.current !== null) {
      window.clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
    longPressStartRef.current = null;
  }, []);

  const onPointerDownCapture = React.useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      if (!onToggle) return;
      if (selectionMode || readOnly) return;
      if (event.button !== 0 || event.shiftKey) return;
      if (shouldIgnoreLongPressTarget(event.target)) return;

      longPressStartRef.current = {
        x: event.clientX,
        y: event.clientY,
      };
      clearLongPress();
      longPressTimerRef.current = window.setTimeout(() => {
        suppressClickUntilRef.current = Date.now() + 450;
        onToggle(false);
      }, 450);
    },
    [clearLongPress, onToggle, readOnly, selectionMode],
  );

  const onPointerMoveCapture = React.useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      if (longPressTimerRef.current === null) return;
      const start = longPressStartRef.current;
      if (!start) return;
      const dx = Math.abs(event.clientX - start.x);
      const dy = Math.abs(event.clientY - start.y);
      if (dx > 8 || dy > 8) {
        clearLongPress();
      }
    },
    [clearLongPress],
  );

  const onClickCapture = React.useCallback((event: React.MouseEvent) => {
    if (Date.now() > suppressClickUntilRef.current) return;
    event.preventDefault();
    event.stopPropagation();
  }, []);

  React.useEffect(() => clearLongPress, [clearLongPress]);

  return {
    onClickCapture,
    onPointerCancelCapture: clearLongPress,
    onPointerDownCapture,
    onPointerMoveCapture,
    onPointerUpCapture: clearLongPress,
  };
};

export const getShiftKey = (event?: React.SyntheticEvent): boolean => {
  const nativeEvent = event?.nativeEvent;
  return nativeEvent instanceof MouseEvent ||
    nativeEvent instanceof KeyboardEvent
    ? nativeEvent.shiftKey
    : false;
};
