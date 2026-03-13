import React from "react";
import { Box, LinearProgress } from "@mui/material";
import { Shuffle } from "@mui/icons-material";
import H5AudioPlayer from "react-h5-audio-player";
import "react-h5-audio-player/lib/styles.css";
import { filesApi } from "../api/filesApi";
import type { AudioPlaylistItem } from "../types/audio";

interface AudioPlayerProps {
  currentFileId: string;
  currentFileName: string;
  playlist?: ReadonlyArray<AudioPlaylistItem> | null;
  onTrackChange?: (item: AudioPlaylistItem) => void;
  shuffleEnabled?: boolean;
  onToggleShuffle?: () => void;
  shuffleLabel?: string;
  onListen?: (timeSeconds: number) => void;
  listenIntervalMs?: number;
}

const EXPIRE_AFTER_MINUTES = 60 * 24;

const buildInlineAudioUrl = (downloadLink: string): string => {
  const url = new URL(downloadLink, window.location.origin);
  url.searchParams.set("download", "false");
  return url.toString();
};

const findIndexById = (
  items: ReadonlyArray<AudioPlaylistItem>,
  id: string,
): number => {
  const index = items.findIndex((item) => item.id === id);
  return index >= 0 ? index : 0;
};

export const AudioPlayer: React.FC<AudioPlayerProps> = ({
  currentFileId,
  currentFileName,
  playlist,
  onTrackChange,
  shuffleEnabled = false,
  onToggleShuffle,
  shuffleLabel,
  onListen,
  listenIntervalMs,
}) => {
  const playerRef = React.useRef<React.ElementRef<typeof H5AudioPlayer>>(null);
  const urlCacheRef = React.useRef<Map<string, string>>(new Map());

  const currentIndexRef = React.useRef<number>(0);
  const shuffleOrderRef = React.useRef<ReadonlyArray<number> | null>(null);
  const shufflePosRef = React.useRef<number>(0);

  const effectivePlaylist = React.useMemo<
    ReadonlyArray<AudioPlaylistItem>
  >(() => {
    const list = playlist ?? [];
    if (list.length > 0 && list.some((item) => item.id === currentFileId)) {
      return list;
    }
    return [{ id: currentFileId, name: currentFileName }];
  }, [currentFileId, currentFileName, playlist]);

  const [currentIndex, setCurrentIndex] = React.useState<number>(() =>
    findIndexById(effectivePlaylist, currentFileId),
  );

  React.useEffect(() => {
    currentIndexRef.current = currentIndex;
  }, [currentIndex]);

  const rebuildShuffleOrder = React.useCallback(
    (targetPlaylist: ReadonlyArray<AudioPlaylistItem>, firstIndex: number) => {
      const length = targetPlaylist.length;
      const safeFirst = Math.min(
        Math.max(0, firstIndex),
        Math.max(0, length - 1),
      );

      const order: number[] = [];
      for (let i = 0; i < length; i += 1) {
        if (i !== safeFirst) {
          order.push(i);
        }
      }

      for (let i = order.length - 1; i > 0; i -= 1) {
        const j = Math.floor(Math.random() * (i + 1));
        const tmp = order[i];
        order[i] = order[j];
        order[j] = tmp;
      }

      shuffleOrderRef.current = [safeFirst, ...order];
      shufflePosRef.current = 0;
    },
    [],
  );

  React.useEffect(() => {
    const nextIndex = findIndexById(effectivePlaylist, currentFileId);
    setCurrentIndex((prev) => (prev === nextIndex ? prev : nextIndex));
  }, [effectivePlaylist, currentFileId]);

  React.useEffect(() => {
    if (!shuffleEnabled) {
      shuffleOrderRef.current = null;
      shufflePosRef.current = 0;
      return;
    }

    const currentIdx = findIndexById(effectivePlaylist, currentFileId);
    const existing = shuffleOrderRef.current;
    const shouldRebuild = !existing || existing.length !== effectivePlaylist.length;

    if (shouldRebuild) {
      rebuildShuffleOrder(effectivePlaylist, currentIdx);
      return;
    }

    const pos = existing.findIndex((i) => i === currentIdx);
    if (pos >= 0) {
      shufflePosRef.current = pos;
    } else {
      rebuildShuffleOrder(effectivePlaylist, currentIdx);
    }
  }, [shuffleEnabled, effectivePlaylist, currentFileId, rebuildShuffleOrder]);

  const safeIndex = Math.min(
    Math.max(0, currentIndex),
    Math.max(0, effectivePlaylist.length - 1),
  );

  const currentItem = effectivePlaylist[safeIndex];

  const [src, setSrc] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState<boolean>(true);

  React.useEffect(() => {
    if (!onListen) {
      return;
    }

    type PlayerWithAudioRef = { audio: React.RefObject<HTMLAudioElement> };
    const intervalMs = listenIntervalMs ?? 250;

    const timerId = window.setInterval(() => {
      const audioEl = (playerRef.current as PlayerWithAudioRef | null)?.audio.current;
      if (!audioEl) return;
      onListen(audioEl.currentTime);
    }, intervalMs);

    return () => {
      window.clearInterval(timerId);
    };
  }, [onListen, listenIntervalMs]);

  React.useEffect(() => {
    let cancelled = false;

    const resolve = async () => {
      setLoading(true);

      const cached = urlCacheRef.current.get(currentItem.id);
      if (cached) {
        setSrc(cached);
        setLoading(false);
        return;
      }

      try {
        const downloadLink = await filesApi.getDownloadLink(
          currentItem.id,
          EXPIRE_AFTER_MINUTES,
        );
        const inlineUrl = buildInlineAudioUrl(downloadLink);
        urlCacheRef.current.set(currentItem.id, inlineUrl);

        if (!cancelled) {
          setSrc(inlineUrl);
        }
      } catch {
        // Keep previous src if present; only clear when there is nothing to play.
        if (!cancelled && !urlCacheRef.current.size) {
          setSrc(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    resolve();

    return () => {
      cancelled = true;
    };
  }, [currentItem.id]);

  const hasPlaylist = effectivePlaylist.length > 1;

  const handlePrevious = React.useCallback(() => {
    if (shuffleEnabled && shuffleOrderRef.current) {
      const order = shuffleOrderRef.current;
      const pos = shufflePosRef.current;
      const nextPos = pos > 0 ? pos - 1 : pos;
      if (nextPos === pos) {
        return;
      }

      shufflePosRef.current = nextPos;
      const nextIndex = order[nextPos] ?? 0;
      setCurrentIndex(nextIndex);
      const nextItem = effectivePlaylist[nextIndex];
      if (nextItem) {
        onTrackChange?.(nextItem);
      }
      return;
    }

    const idx = currentIndexRef.current;
    const nextIndex = idx > 0 ? idx - 1 : idx;
    if (nextIndex === idx) {
      return;
    }

    setCurrentIndex(nextIndex);
    const nextItem = effectivePlaylist[nextIndex];
    if (nextItem) {
      onTrackChange?.(nextItem);
    }
  }, [effectivePlaylist, onTrackChange, shuffleEnabled]);

  const handleNext = React.useCallback(() => {
    if (shuffleEnabled && shuffleOrderRef.current) {
      const order = shuffleOrderRef.current;
      const pos = shufflePosRef.current;
      const nextPos = pos + 1 < order.length ? pos + 1 : pos;
      if (nextPos === pos) {
        return;
      }

      shufflePosRef.current = nextPos;
      const nextIndex = order[nextPos] ?? 0;
      setCurrentIndex(nextIndex);
      const nextItem = effectivePlaylist[nextIndex];
      if (nextItem) {
        onTrackChange?.(nextItem);
      }
      return;
    }

    const idx = currentIndexRef.current;
    const nextIndex = idx + 1 < effectivePlaylist.length ? idx + 1 : idx;
    if (nextIndex === idx) {
      return;
    }

    setCurrentIndex(nextIndex);
    const nextItem = effectivePlaylist[nextIndex];
    if (nextItem) {
      onTrackChange?.(nextItem);
    }
  }, [effectivePlaylist, onTrackChange, shuffleEnabled]);

  const handleEnded = React.useCallback(() => {
    if (shuffleEnabled && shuffleOrderRef.current) {
      const order = shuffleOrderRef.current;
      const pos = shufflePosRef.current;
      const nextPos = pos + 1 < order.length ? pos + 1 : pos;
      if (nextPos === pos) {
        return;
      }

      shufflePosRef.current = nextPos;
      const nextIndex = order[nextPos] ?? 0;
      setCurrentIndex(nextIndex);
      const nextItem = effectivePlaylist[nextIndex];
      if (nextItem) {
        onTrackChange?.(nextItem);
      }
      return;
    }

    const idx = currentIndexRef.current;
    const nextIndex = idx + 1 < effectivePlaylist.length ? idx + 1 : idx;
    if (nextIndex === idx) {
      return;
    }

    setCurrentIndex(nextIndex);
    const nextItem = effectivePlaylist[nextIndex];
    if (nextItem) {
      onTrackChange?.(nextItem);
    }
  }, [effectivePlaylist, onTrackChange, shuffleEnabled]);

  return (
    <Box
      width="100%"
      position="relative"
      sx={(theme) => ({
        "& .rhap_container": {
          backgroundColor: "transparent",
          boxShadow: "none",
          padding: 0,
        },
        "& .rhap_time": {
          color: theme.palette.text.secondary,
        },
        "& .rhap_progress-indicator": {
          backgroundColor: theme.palette.primary.main,
        },
        "& .rhap_progress-filled": {
          backgroundColor: theme.palette.primary.main,
        },
        "& .rhap_volume-indicator": {
          backgroundColor: theme.palette.primary.main,
        },
        "& .rhap_volume-filled": {
          backgroundColor: theme.palette.primary.main,
        },
        "& .rhap_main-controls-button, & .rhap_volume-button": {
          color: theme.palette.text.primary,
        },
        "& .rhap_repeat-button, & .rhap_forward-button, & .rhap_rewind-button":
          {
            color: theme.palette.text.primary,
          },
        "& .rhap_button-clear": {
          WebkitTapHighlightColor: "transparent",
        },
        "& .rhap_button-clear:focus, & .rhap_button-clear:focus-visible": {
          outline: "none",
          boxShadow: "none",
        },
        "& .rhap_progress-indicator:focus, & .rhap_progress-indicator:focus-visible": {
          outline: "none",
          boxShadow: "none",
        },
        "& .ctn-shuffle-button": {
          color: theme.palette.text.secondary,
        },
        "& .ctn-shuffle-button.ctn-shuffle-active": {
          color: theme.palette.primary.main,
        },
      })}
    >
      {loading && (
        <Box
          position="absolute"
          top={8}
          right={8}
          zIndex={1}
          display="flex"
          alignItems="center"
          justifyContent="center"
        >
          <LinearProgress />
        </Box>
      )}

      <H5AudioPlayer
        ref={playerRef}
        src={src ?? ""}
        autoPlay
        autoPlayAfterSrcChange
        showSkipControls={hasPlaylist}
        showJumpControls={false}
        onClickPrevious={hasPlaylist ? handlePrevious : undefined}
        onClickNext={hasPlaylist ? handleNext : undefined}
        onEnded={handleEnded}
        preload="metadata"
        customAdditionalControls={
          onToggleShuffle
            ? [
                <button
                  key="shuffle"
                  type="button"
                  className={`rhap_button-clear rhap_repeat-button ctn-shuffle-button${
                    shuffleEnabled ? " ctn-shuffle-active" : ""
                  }`}
                  onClick={onToggleShuffle}
                  aria-label={shuffleLabel}
                  title={shuffleLabel}
                >
                  <Shuffle fontSize="small" />
                </button>,
              ]
            : undefined
        }
      />
    </Box>
  );
};
