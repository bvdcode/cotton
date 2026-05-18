import { useEffect, useMemo, useRef, useState } from "react";
import type { CSSProperties } from "react";
import { useMediaQuery } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import type { TilesSize } from "@shared/types/FileListViewTypes";

interface TileLayout {
  minWidth: string;
  gap: number;
}

const DEFAULT_COLUMNS_FALLBACK = 2;

const tryParsePx = (value: string): number | null => {
  const trimmed = value.trim();
  if (!trimmed.endsWith("px")) return null;
  const parsed = Number.parseFloat(trimmed.slice(0, -2));
  return Number.isFinite(parsed) ? parsed : null;
};

const findScrollableParent = (
  element: HTMLElement | null,
): HTMLElement | null => {
  let current: HTMLElement | null = element?.parentElement ?? null;

  while (current) {
    const style = window.getComputedStyle(current);
    const overflowY = style.overflowY;
    const overflow = style.overflow;

    const isScrollable =
      overflowY === "auto" ||
      overflowY === "scroll" ||
      overflow === "auto" ||
      overflow === "scroll";

    if (isScrollable) {
      return current;
    }

    current = current.parentElement;
  }

  return null;
};

const getTileLayout = (
  tileSize: TilesSize,
  isXs: boolean,
  spacing: (factor: number) => string,
): TileLayout => {
  if (isXs) {
    switch (tileSize) {
      case "small":
        return { minWidth: "80px", gap: 6 };
      case "medium":
        return { minWidth: "112px", gap: 8 };
      case "large":
        return { minWidth: "44%", gap: 10 };
    }
  }

  switch (tileSize) {
    case "small":
      return { minWidth: spacing(14), gap: 8 };
    case "medium":
      return { minWidth: spacing(19), gap: 12 };
    case "large":
      return { minWidth: spacing(26), gap: 16 };
  }
};

export const useTileGridLayout = (tileSize: TilesSize) => {
  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [containerWidth, setContainerWidth] = useState<number>(0);
  const [scrollParent, setScrollParent] = useState<HTMLElement | null>(null);

  const layout = useMemo(
    () => getTileLayout(tileSize, isXs, theme.spacing),
    [isXs, theme.spacing, tileSize],
  );

  useEffect(() => {
    const el = containerRef.current;
    if (!el || typeof ResizeObserver === "undefined") return;

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) return;
      setContainerWidth(entry.contentRect.width);
    });

    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    setScrollParent(findScrollableParent(el));
  }, []);

  const columns = useMemo(() => {
    if (tileSize === "large" && isXs && layout.minWidth.trim().endsWith("%")) {
      return 2;
    }

    const minWidthPx = tryParsePx(layout.minWidth);
    if (!minWidthPx || containerWidth <= 0) {
      return DEFAULT_COLUMNS_FALLBACK;
    }

    const calculated = Math.floor(
      (containerWidth + layout.gap) / (minWidthPx + layout.gap),
    );
    return Math.max(1, calculated);
  }, [containerWidth, isXs, layout.gap, layout.minWidth, tileSize]);

  const gridStyles = useMemo<CSSProperties>(
    () => ({
      display: "grid",
      gap: `${layout.gap}px`,
      gridTemplateColumns: `repeat(auto-fill, minmax(${layout.minWidth}, 1fr))`,
    }),
    [layout.gap, layout.minWidth],
  );

  return {
    containerRef,
    scrollParent,
    columns,
    gapPx: layout.gap,
    gridStyles,
  };
};
