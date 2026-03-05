import React from "react";
import { Box, LinearProgress } from "@mui/material";
import H5AudioPlayer from "react-h5-audio-player";
import "react-h5-audio-player/lib/styles.css";
import { filesApi } from "../api/filesApi";
import type { AudioPlaylistItem } from "../types/audio";

interface AudioPlayerProps {
  currentFileId: string;
  currentFileName: string;
  playlist?: ReadonlyArray<AudioPlaylistItem> | null;
  onTrackChange?: (item: AudioPlaylistItem) => void;
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
}) => {
  const urlCacheRef = React.useRef<Map<string, string>>(new Map());

  const currentIndexRef = React.useRef<number>(0);

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

  React.useEffect(() => {
    const nextIndex = findIndexById(effectivePlaylist, currentFileId);
    setCurrentIndex((prev) => (prev === nextIndex ? prev : nextIndex));
  }, [effectivePlaylist, currentFileId]);

  const safeIndex = Math.min(
    Math.max(0, currentIndex),
    Math.max(0, effectivePlaylist.length - 1),
  );

  const currentItem = effectivePlaylist[safeIndex];

  const [src, setSrc] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState<boolean>(true);

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
  }, [effectivePlaylist, onTrackChange]);

  const handleNext = React.useCallback(() => {
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
  }, [effectivePlaylist, onTrackChange]);

  const handleEnded = React.useCallback(() => {
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
  }, [effectivePlaylist, onTrackChange]);

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
        src={src ?? ""}
        autoPlay
        autoPlayAfterSrcChange
        showSkipControls={hasPlaylist}
        showJumpControls={false}
        onClickPrevious={hasPlaylist ? handlePrevious : undefined}
        onClickNext={hasPlaylist ? handleNext : undefined}
        onEnded={handleEnded}
        preload="metadata"
      />
    </Box>
  );
};
