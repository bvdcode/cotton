import React from "react";

export const useAudioPlayerBarOffset = (open: boolean) => {
  const paperRef = React.useRef<HTMLDivElement | null>(null);

  React.useLayoutEffect(() => {
    const root = document.documentElement;

    if (!open) {
      root.style.setProperty("--audio-player-bar-offset", "0px");
      return;
    }

    const el = paperRef.current;
    if (!el) {
      return;
    }

    const update = () => {
      const heightPx = Math.ceil(el.getBoundingClientRect().height);
      root.style.setProperty(
        "--audio-player-bar-offset",
        `calc(${heightPx}px + env(safe-area-inset-bottom, 0px))`,
      );
    };

    update();

    if (typeof ResizeObserver === "undefined") {
      return;
    }

    const ro = new ResizeObserver(() => update());
    ro.observe(el);

    return () => ro.disconnect();
  }, [open]);

  return paperRef;
};
