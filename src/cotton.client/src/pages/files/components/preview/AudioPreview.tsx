import React from "react";
import { Box, CircularProgress } from "@mui/material";
import H5AudioPlayer from "react-h5-audio-player";
import "react-h5-audio-player/lib/styles.css";
import { filesApi } from "../../../../shared/api/filesApi";

export interface AudioPlaylistItem {
  id: string;
  name: string;
}

interface AudioPreviewProps {
  nodeFileId: string;
  fileName: string;
  playlist?: ReadonlyArray<AudioPlaylistItem> | null;
}

const EXPIRE_AFTER_MINUTES = 60 * 24;

const buildInlineAudioUrl = (downloadLink: string): string => {
  const url = new URL(downloadLink, window.location.origin);
  // Backend supports inline for at least PDFs; try the same flag for audio.
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

export const AudioPreview: React.FC<AudioPreviewProps> = ({
  nodeFileId,
  fileName,
  playlist,
}) => {
  const urlCacheRef = React.useRef<Map<string, string>>(new Map());

  const effectivePlaylist = React.useMemo<ReadonlyArray<AudioPlaylistItem>>(() => {
    const list = playlist ?? [];
    if (list.length > 0 && list.some((item) => item.id === nodeFileId)) {
      return list;
    }
    return [{ id: nodeFileId, name: fileName }];
  }, [fileName, nodeFileId, playlist]);

  const [currentIndex, setCurrentIndex] = React.useState<number>(() =>
    findIndexById(effectivePlaylist, nodeFileId),
  );

  React.useEffect(() => {
    setCurrentIndex(findIndexById(effectivePlaylist, nodeFileId));
  }, [effectivePlaylist, nodeFileId]);

  const currentItem = effectivePlaylist[currentIndex];

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
        if (!cancelled) {
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
  }, [currentItem.id, currentItem.name]);

  const hasPlaylist = effectivePlaylist.length > 1;

  const handlePrevious = React.useCallback(() => {
    setCurrentIndex((idx) => (idx > 0 ? idx - 1 : idx));
  }, []);

  const handleNext = React.useCallback(() => {
    setCurrentIndex((idx) =>
      idx + 1 < effectivePlaylist.length ? idx + 1 : idx,
    );
  }, [effectivePlaylist.length]);

  const handleEnded = React.useCallback(() => {
    setCurrentIndex((idx) =>
      idx + 1 < effectivePlaylist.length ? idx + 1 : idx,
    );
  }, [effectivePlaylist.length]);

  return (
    <Box
      width="100%"
      sx={(theme) => ({
        // Theme the 3rd-party player without hardcoding colors.
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
        "& .rhap_repeat-button, & .rhap_forward-button, & .rhap_rewind-button": {
          color: theme.palette.text.primary,
        },
      })}
    >
      {loading && (
        <Box display="flex" alignItems="center" justifyContent="center" py={1}>
          <CircularProgress size={18} />
        </Box>
      )}

      {!loading && src && (
        <H5AudioPlayer
          src={src}
          autoPlay
          autoPlayAfterSrcChange
          showSkipControls={hasPlaylist}
          showJumpControls={false}
          onClickPrevious={hasPlaylist ? handlePrevious : undefined}
          onClickNext={hasPlaylist ? handleNext : undefined}
          onEnded={handleEnded}
          preload="metadata"
        />
      )}
    </Box>
  );
};
