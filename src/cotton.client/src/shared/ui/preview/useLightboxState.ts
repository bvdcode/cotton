import React from "react";
import type { MediaLightboxProps } from "@shared/types/mediaLightbox";

type LightboxIndexState = {
  key: string;
  index: number;
};

type IndexOrUpdater = number | ((current: number) => number);

type ActiveVideoState = {
  key: string;
  fileId: string;
  element: HTMLVideoElement;
};

const buildLightboxIndexKey = (
  open: boolean,
  initialIndex: number,
  items: MediaLightboxProps["items"],
): string => {
  if (!open) {
    return "closed";
  }

  return [initialIndex, items[initialIndex]?.id ?? ""].join("\u0000");
};

const resolveIndex = (current: number, next: IndexOrUpdater): number => {
  return typeof next === "function" ? next(current) : next;
};

export const useLightboxIndex = (
  open: boolean,
  initialIndex: number,
  items: MediaLightboxProps["items"],
) => {
  const indexKey = buildLightboxIndexKey(open, initialIndex, items);
  const [indexState, setIndexState] = React.useState<LightboxIndexState>(
    () => ({
      key: indexKey,
      index: initialIndex,
    }),
  );
  const rawIndex =
    indexState.key === indexKey ? indexState.index : initialIndex;
  const index = items.length === 0 ? 0 : Math.min(rawIndex, items.length - 1);
  const setLightboxIndex = React.useCallback(
    (next: IndexOrUpdater) => {
      setIndexState((currentState) => {
        const current =
          currentState.key === indexKey
            ? currentState
            : { key: indexKey, index: initialIndex };
        const nextIndex = resolveIndex(current.index, next);
        if (nextIndex === current.index) {
          return currentState.key === indexKey ? currentState : current;
        }

        return { key: indexKey, index: nextIndex };
      });
    },
    [indexKey, initialIndex],
  );

  return { index, indexKey, setLightboxIndex };
};

export const useActiveVideoElement = (
  indexKey: string,
  currentItemId: string | null,
) => {
  const [activeVideoState, setActiveVideoState] =
    React.useState<ActiveVideoState | null>(null);
  const activeVideoElement =
    activeVideoState?.key === indexKey &&
    activeVideoState.fileId === currentItemId
      ? activeVideoState.element
      : null;
  const setActiveVideoElementForFile = React.useCallback(
    (fileId: string, element: HTMLVideoElement | null) => {
      setActiveVideoState((current) => {
        if (!element) {
          return current?.key === indexKey && current.fileId === fileId
            ? null
            : current;
        }

        return { key: indexKey, fileId, element };
      });
    },
    [indexKey],
  );

  return {
    activeVideoElement,
    setActiveVideoElementForFile,
    setActiveVideoState,
  };
};
