import { useLayoutEffect, useRef } from "react";
import type { RefObject } from "react";
import { useMediaQuery, useTheme } from "@mui/material";

interface TilePosition {
  element: HTMLElement;
  left: number;
  top: number;
}

function readPositions(
  host: HTMLElement,
  animations: ReadonlyMap<string, Animation>,
): Map<string, TilePosition> {
  const origin = host.getBoundingClientRect();
  const positions = new Map<string, TilePosition>();
  for (const element of host.querySelectorAll<HTMLElement>("[data-tile-id]")) {
    const id = element.dataset.tileId;
    if (id === undefined) {
      continue;
    }
    const rect = element.getBoundingClientRect();
    let x = 0;
    let y = 0;
    if (animations.get(id)?.playState === "running") {
      const offset = new DOMMatrixReadOnly(getComputedStyle(element).transform);
      x = offset.m41;
      y = offset.m42;
    }
    positions.set(id, {
      element,
      left: rect.left - origin.left - x,
      top: rect.top - origin.top - y,
    });
  }
  return positions;
}

export function useTileMovement(
  container: RefObject<HTMLElement | null>,
  ids: readonly string[],
  leadingItems: number,
) {
  const theme = useTheme();
  const reducedMotion = useMediaQuery("(prefers-reduced-motion: reduce)");
  const previous = useRef(new Map<string, TilePosition>());
  const order = useRef({ ids, leadingItems });
  const animations = useRef(new Map<string, Animation>());
  const pendingFrame = useRef<number | null>(null);

  useLayoutEffect(() => {
    const host = container.current;
    if (!host) {
      return;
    }
    const capture = () => {
      if (pendingFrame.current === null) {
        previous.current = readPositions(host, animations.current);
      }
    };
    const observer = new MutationObserver(capture);
    observer.observe(host, { childList: true, subtree: true });
    const resize = new ResizeObserver(capture);
    resize.observe(host);
    const active = animations.current;
    return () => {
      if (pendingFrame.current !== null) {
        cancelAnimationFrame(pendingFrame.current);
      }
      observer.disconnect();
      resize.disconnect();
      for (const animation of active.values()) {
        animation.cancel();
      }
      active.clear();
    };
  }, [container]);

  useLayoutEffect(() => {
    const host = container.current;
    if (!host) {
      return;
    }
    const changed =
      order.current.leadingItems !== leadingItems ||
      order.current.ids.length !== ids.length ||
      order.current.ids.some((id, index) => id !== ids[index]);
    order.current = { ids, leadingItems };
    if (!changed) {
      if (pendingFrame.current === null) {
        previous.current = readPositions(host, animations.current);
      }
      return;
    }
    if (pendingFrame.current !== null) {
      cancelAnimationFrame(pendingFrame.current);
    }
    pendingFrame.current = requestAnimationFrame(() => {
      pendingFrame.current = null;
      const next = readPositions(host, animations.current);
      for (const [id, position] of next) {
        const before = previous.current.get(id);
        const active = animations.current.get(id);
        let offsetX = 0;
        let offsetY = 0;
        if (before && active?.playState === "running") {
          const offset = new DOMMatrixReadOnly(
            getComputedStyle(before.element).transform,
          );
          offsetX = offset.m41;
          offsetY = offset.m42;
        }
        active?.cancel();
        if (reducedMotion || !before) {
          continue;
        }
        const x = before.left + offsetX - position.left;
        const y = before.top + offsetY - position.top;
        if (x === 0 && y === 0) {
          continue;
        }
        animations.current.set(
          id,
          position.element.animate(
            [
              { transform: `translate(${x}px, ${y}px)` },
              { transform: "translate(0, 0)" },
            ],
            {
              duration: theme.transitions.duration.standard,
              easing: theme.transitions.easing.easeOut,
            },
          ),
        );
      }
      for (const [id, animation] of animations.current) {
        if (!next.has(id)) {
          animation.cancel();
          animations.current.delete(id);
        }
      }
      previous.current = next;
    });
  }, [container, ids, leadingItems, reducedMotion, theme]);
}
