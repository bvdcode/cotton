import { useEffect, useState, useRef } from "react";

/**
 * Hook for tracking user activity with auto-hide functionality
 * 
 * Single Responsibility: Only handles activity detection and state
 * 
 * @param timeout - Milliseconds of inactivity before hiding (default: 2500ms)
 * @returns isActive - Whether user is currently active
 */
export function useActivityDetection(timeout: number = 2500): boolean {
  const [isActive, setIsActive] = useState(true);
  const timeoutRef = useRef<number | null>(null);

  useEffect(() => {
    const resetActivity = () => {
      setIsActive(true);

      if (timeoutRef.current !== null) {
        window.clearTimeout(timeoutRef.current);
      }

      timeoutRef.current = window.setTimeout(() => {
        setIsActive(false);
      }, timeout);
    };

    // Start as active when mounted
    resetActivity();

    // Listen to all user interaction events
    const events: (keyof DocumentEventMap)[] = [
      "mousemove",
      "mousedown",
      "keydown",
      "wheel",
      "touchstart",
      "touchmove",
    ];

    events.forEach((event) => {
      document.addEventListener(event, resetActivity, { passive: true });
    });

    return () => {
      if (timeoutRef.current !== null) {
        window.clearTimeout(timeoutRef.current);
      }
      events.forEach((event) => {
        document.removeEventListener(event, resetActivity);
      });
    };
  }, [timeout]);

  return isActive;
}
