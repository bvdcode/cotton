import React from "react";
import { useTrackLyricsQuery } from "../../shared/api/queries/audio";
import { findActiveLrcLineIndex, type LrcLine } from "../../shared/utils/lrc";

type LyricsStatus = "idle" | "loading" | "ready" | "notFound" | "error";

type LyricsPlaybackState = {
  key: string;
  activeIndex: number;
  countdown: number | null;
  started: boolean;
  countdownConsumed: boolean;
};

const createLyricsPlaybackState = (key: string): LyricsPlaybackState => ({
  key,
  activeIndex: 0,
  countdown: null,
  started: false,
  countdownConsumed: false,
});

const buildLyricsPlaybackKey = (
  fileId: string | null,
  lines: ReadonlyArray<LrcLine>,
): string => {
  const firstLineTime = lines[0]?.timeSeconds ?? "";
  const lastLineTime = lines[lines.length - 1]?.timeSeconds ?? "";
  return [fileId ?? "", lines.length, firstLineTime, lastLineTime].join(":");
};

export const useAudioLyricsPlayback = (options: {
  currentFileId: string | null;
  currentFileName: string | null;
  currentItem: { name: string; nodeId?: string } | null;
  effectiveLyricsOpen: boolean;
}) => {
  const { currentFileId, currentFileName, currentItem, effectiveLyricsOpen } =
    options;
  const [lyricsPlaybackState, setLyricsPlaybackState] =
    React.useState<LyricsPlaybackState>(() => createLyricsPlaybackState(""));
  const lyricsAudioFileName = currentItem?.name ?? currentFileName;
  const lyricsQuery = useTrackLyricsQuery({
    folderNodeId: currentItem?.nodeId ?? null,
    audioFileName: lyricsAudioFileName,
    enabled: effectiveLyricsOpen,
  });
  const lyricsLines = React.useMemo<ReadonlyArray<LrcLine>>(
    () => lyricsQuery.data ?? [],
    [lyricsQuery.data],
  );
  const lyricsStatus: LyricsStatus = lyricsQuery.isPending
    ? effectiveLyricsOpen
      ? "loading"
      : "idle"
    : lyricsQuery.isError
      ? "error"
      : lyricsLines.length > 0
        ? "ready"
        : "notFound";

  const lyricsPlaybackKey = React.useMemo(
    () => buildLyricsPlaybackKey(currentFileId, lyricsLines),
    [currentFileId, lyricsLines],
  );
  const lyricsPlayback =
    lyricsPlaybackState.key === lyricsPlaybackKey
      ? lyricsPlaybackState
      : createLyricsPlaybackState(lyricsPlaybackKey);
  const lyricsListenEnabled = effectiveLyricsOpen && lyricsLines.length > 0;

  const handleListen = React.useCallback(
    (timeSeconds: number) => {
      if (!lyricsListenEnabled) return;

      const firstTime = lyricsLines[0]?.timeSeconds;
      if (typeof firstTime !== "number") {
        return;
      }

      setLyricsPlaybackState((previous) => {
        const current =
          previous.key === lyricsPlaybackKey
            ? previous
            : createLyricsPlaybackState(lyricsPlaybackKey);
        const started = timeSeconds >= firstTime;

        if (started) {
          const nextActiveIndex = findActiveLrcLineIndex(
            lyricsLines,
            timeSeconds,
          );
          if (
            current.started &&
            current.countdown === null &&
            current.countdownConsumed &&
            current.activeIndex === nextActiveIndex
          ) {
            return current;
          }

          return {
            ...current,
            activeIndex: nextActiveIndex,
            countdown: null,
            started: true,
            countdownConsumed: true,
          };
        }

        if (current.countdownConsumed) {
          if (!current.started && current.countdown === null) {
            return current;
          }

          return {
            ...current,
            countdown: null,
            started: false,
          };
        }

        const delta = firstTime - timeSeconds;

        if (delta > 3) {
          if (!current.started && current.countdown === null) {
            return current;
          }

          return {
            ...current,
            countdown: null,
            started: false,
          };
        }

        const safeDelta = Math.max(0.0001, delta);
        const nextCountdown = Math.ceil(safeDelta);
        if (!current.started && current.countdown === nextCountdown) {
          return current;
        }

        return {
          ...current,
          countdown: nextCountdown,
          started: false,
        };
      });
    },
    [lyricsLines, lyricsListenEnabled, lyricsPlaybackKey],
  );

  return {
    lyricsLines,
    lyricsStatus,
    lyricsPlayback,
    lyricsListenEnabled,
    handleListen,
  };
};
