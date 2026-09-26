import React from "react";

const TOUCH_CONTROLS_AUTOHIDE_MS = 2500;

export const useLightboxTouchControls = (
  open: boolean,
  isTouchDevice: boolean,
) => {
  const [touchControlsVisible, setTouchControlsVisible] =
    React.useState<boolean>(true);
  const touchControlsTimerRef = React.useRef<number | null>(null);

  const clearTouchControlsTimer = React.useCallback(() => {
    if (touchControlsTimerRef.current !== null) {
      window.clearTimeout(touchControlsTimerRef.current);
      touchControlsTimerRef.current = null;
    }
  }, []);

  const showTouchControls = React.useCallback(() => {
    if (!isTouchDevice) return;

    setTouchControlsVisible(true);
    clearTouchControlsTimer();

    touchControlsTimerRef.current = window.setTimeout(() => {
      setTouchControlsVisible(false);
      touchControlsTimerRef.current = null;
    }, TOUCH_CONTROLS_AUTOHIDE_MS);
  }, [clearTouchControlsTimer, isTouchDevice]);

  const toggleTouchControls = React.useCallback(() => {
    if (!isTouchDevice) return;

    setTouchControlsVisible((previous) => {
      const next = !previous;
      clearTouchControlsTimer();

      if (next) {
        touchControlsTimerRef.current = window.setTimeout(() => {
          setTouchControlsVisible(false);
          touchControlsTimerRef.current = null;
        }, TOUCH_CONTROLS_AUTOHIDE_MS);
      }

      return next;
    });
  }, [clearTouchControlsTimer, isTouchDevice]);

  React.useEffect(() => {
    if (!open || !isTouchDevice) {
      return;
    }

    clearTouchControlsTimer();
    touchControlsTimerRef.current = window.setTimeout(() => {
      setTouchControlsVisible(false);
      touchControlsTimerRef.current = null;
    }, TOUCH_CONTROLS_AUTOHIDE_MS);

    return () => {
      clearTouchControlsTimer();
    };
  }, [open, isTouchDevice, clearTouchControlsTimer]);

  return {
    touchControlsVisible,
    showTouchControls,
    toggleTouchControls,
    setTouchControlsVisible,
  };
};
