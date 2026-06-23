import React from "react";
import {
  Box,
  Collapse,
  CircularProgress,
  Divider,
  IconButton,
  LinearProgress,
  List,
  ListItemButton,
  Paper,
  Snackbar,
  Tooltip,
  Typography,
  useMediaQuery,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import {
  Close,
  QueueMusic,
  Subtitles,
  TravelExplore,
} from "@mui/icons-material";
import type { SnackbarCloseReason } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
  selectAudioPlayerCurrentFileId,
  selectAudioPlayerCurrentFileName,
  selectAudioPlayerIsScanning,
  selectAudioPlayerOpen,
  selectAudioPlayerPlaylist,
  selectAudioPlayerShuffleEnabled,
  useAudioPlayerStore,
} from "../../shared/store/audioPlayerStore";
import { useTrackLyricsQuery } from "../../shared/api/queries/audio";
import { AudioPlayer } from "../../shared/ui/AudioPlayer";
import { AudioLyricsView } from "../../shared/ui/AudioLyricsView";
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

export const AudioPlayerBar: React.FC = () => {
  const { t } = useTranslation(["audioPlayer"]);

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const lyricsLineHeightPx = isMobile ? 44 : 32;
  const lyricsViewHeightPx = lyricsLineHeightPx * 3;

  const open = useAudioPlayerStore(selectAudioPlayerOpen);
  const isScanning = useAudioPlayerStore(selectAudioPlayerIsScanning);
  const playlist = useAudioPlayerStore(selectAudioPlayerPlaylist);
  const currentFileId = useAudioPlayerStore(selectAudioPlayerCurrentFileId);
  const currentFileName = useAudioPlayerStore(selectAudioPlayerCurrentFileName);
  const shuffleEnabled = useAudioPlayerStore(selectAudioPlayerShuffleEnabled);

  const close = useAudioPlayerStore((s) => s.close);
  const scanRecursively = useAudioPlayerStore((s) => s.scanRecursively);
  const setCurrentTrack = useAudioPlayerStore((s) => s.setCurrentTrack);
  const toggleShuffle = useAudioPlayerStore((s) => s.toggleShuffle);

  const [lyricsOpen, setLyricsOpen] = React.useState<boolean>(false);
  const toggleLyricsOpen = React.useCallback(
    () => setLyricsOpen((prev) => !prev),
    [],
  );
  const [queueOpen, setQueueOpen] = React.useState<boolean>(false);
  const [lyricsPlaybackState, setLyricsPlaybackState] =
    React.useState<LyricsPlaybackState>(() => createLyricsPlaybackState(""));

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

  const playlistTotal = playlist.length;
  const currentIndex = React.useMemo<number>(() => {
    const index = playlist.findIndex((x) => x.id === currentFileId);
    return index >= 0 ? index : 0;
  }, [playlist, currentFileId]);

  const currentPreviewUrl = React.useMemo<string | undefined>(() => {
    const current = playlist.find((x) => x.id === currentFileId);
    return current?.previewUrl;
  }, [playlist, currentFileId]);

  const currentItem = React.useMemo(() => {
    return playlist.find((x) => x.id === currentFileId) ?? null;
  }, [playlist, currentFileId]);

  const [failedCoverPreviewUrl, setFailedCoverPreviewUrl] = React.useState<
    string | null
  >(null);
  const coverFailed =
    currentPreviewUrl !== undefined &&
    failedCoverPreviewUrl === currentPreviewUrl;
  const effectiveLyricsOpen = open && lyricsOpen;

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

  const positionLabel =
    playlistTotal > 1 ? `${currentIndex + 1}/${playlistTotal}` : null;

  const handleClosePlayer = React.useCallback(() => {
    setLyricsOpen(false);
    close();
  }, [close]);

  const handleClose = (
    _: Event | React.SyntheticEvent<Element, Event>,
    reason?: SnackbarCloseReason,
  ) => {
    if (reason === "clickaway") return;
    handleClosePlayer();
  };

  if (!open || !currentFileId || !currentFileName) {
    return null;
  }

  return (
    <Snackbar
      open={open}
      onClose={handleClose}
      anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      sx={{
        bottom: 0,
        left: 0,
        right: 0,
        transform: "none",
        display: "flex",
        justifyContent: "center",
        px: { xs: 0, sm: 2 },
        pb: "env(safe-area-inset-bottom, 0px)",
        "&.MuiSnackbar-anchorOriginBottomCenter": {
          bottom: 0,
          left: 0,
          right: 0,
          transform: "none",
        },
      }}
    >
      <Paper
        ref={paperRef}
        elevation={8}
        sx={{
          width: "100%",
          maxWidth: 920,
          borderRadius: { xs: 0, sm: 2 },
          bgcolor: "background.paper",
          border: 1,
          borderColor: "divider",
          overflow: "hidden",
        }}
      >
        <Box display="flex" alignItems="center" gap={1} px={2} pt={1}>
          <Box display="flex" alignItems="center" gap={1} flex={1} minWidth={0}>
            {currentPreviewUrl && !coverFailed ? (
              <Box
                component="img"
                src={currentPreviewUrl}
                alt=""
                onError={() => setFailedCoverPreviewUrl(currentPreviewUrl)}
                width={28}
                height={28}
                borderRadius={0.5}
                flexShrink={0}
                sx={{ objectFit: "cover" }}
              />
            ) : null}

            <Typography variant="subtitle2" noWrap flex={1} minWidth={0}>
              {currentFileName}
            </Typography>

            {positionLabel && (
              <Typography variant="caption" color="text.secondary" noWrap>
                {positionLabel}
              </Typography>
            )}
          </Box>

          <Tooltip
            title={
              queueOpen
                ? t("audioPlayer:actions.hideQueue")
                : t("audioPlayer:actions.showQueue")
            }
            arrow
          >
            <span>
              <IconButton
                size="small"
                onClick={() => setQueueOpen((prev) => !prev)}
                color={queueOpen ? "primary" : "default"}
                aria-label={
                  queueOpen
                    ? t("audioPlayer:actions.hideQueue")
                    : t("audioPlayer:actions.showQueue")
                }
              >
                <QueueMusic fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>

          <Tooltip
            title={
              lyricsOpen
                ? t("audioPlayer:actions.hideLyrics")
                : t("audioPlayer:actions.showLyrics")
            }
            arrow
          >
            <span>
              <IconButton
                size="small"
                onClick={() => toggleLyricsOpen()}
                aria-label={
                  lyricsOpen
                    ? t("audioPlayer:actions.hideLyrics")
                    : t("audioPlayer:actions.showLyrics")
                }
                color={lyricsOpen ? "primary" : "default"}
              >
                <Subtitles fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>

          <Tooltip title={t("audioPlayer:actions.scanRecursively")} arrow>
            <span>
              <IconButton
                size="small"
                onClick={() => {
                  void scanRecursively();
                }}
                disabled={isScanning}
                aria-label={t("audioPlayer:actions.scanRecursively")}
              >
                {isScanning ? (
                  <CircularProgress size={18} />
                ) : (
                  <TravelExplore fontSize="small" />
                )}
              </IconButton>
            </span>
          </Tooltip>

          <Tooltip title={t("audioPlayer:actions.close")} arrow>
            <span>
              <IconButton
                size="small"
                onClick={handleClosePlayer}
                aria-label={t("audioPlayer:actions.close")}
              >
                <Close fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
        </Box>

        <Collapse in={effectiveLyricsOpen} timeout="auto" unmountOnExit>
          <Divider sx={{ mt: 1 }} />
          <Box px={2} py={1.25}>
            {!currentItem?.nodeId ? (
              <Typography variant="caption" color="text.secondary">
                {t("audioPlayer:lyrics.unavailable")}
              </Typography>
            ) : lyricsStatus === "loading" ? (
              <LinearProgress />
            ) : lyricsLines.length > 0 ? (
              <Box position="relative" height={lyricsViewHeightPx}>
                <AudioLyricsView
                  lines={lyricsLines}
                  activeIndex={
                    lyricsPlayback.started ? lyricsPlayback.activeIndex : 0
                  }
                  isActivePlaying={lyricsPlayback.started}
                />

                {lyricsPlayback.countdown !== null ? (
                  <Box
                    position="absolute"
                    top={0}
                    left={0}
                    right={0}
                    bottom={0}
                    display="flex"
                    alignItems="center"
                    justifyContent="center"
                    sx={{ pointerEvents: "none" }}
                  >
                    <Typography
                      variant="h4"
                      fontWeight={800}
                      textAlign="center"
                      sx={{ opacity: 0.8 }}
                    >
                      {lyricsPlayback.countdown}
                    </Typography>
                  </Box>
                ) : null}
              </Box>
            ) : lyricsStatus === "error" ? (
              <Typography variant="caption" color="text.secondary">
                {t("audioPlayer:lyrics.loadFailed")}
              </Typography>
            ) : (
              <Typography variant="caption" color="text.secondary">
                {t("audioPlayer:lyrics.notFound")}
              </Typography>
            )}
          </Box>
        </Collapse>

        <Collapse in={queueOpen} timeout="auto" unmountOnExit>
          <Divider sx={{ mt: 1 }} />
          <Box maxHeight={{ xs: 220, sm: 300 }} overflow="auto">
            <List dense disablePadding>
              {playlist.map((item) => (
                <ListItemButton
                  key={item.id}
                  selected={item.id === currentFileId}
                  onClick={() => {
                    setCurrentTrack(item);
                    setQueueOpen(false);
                  }}
                  sx={{
                    "&.Mui-selected": {
                      bgcolor: "action.hover",
                    },
                    "&.Mui-selected:hover": {
                      bgcolor: "action.selected",
                    },
                  }}
                >
                  <Box
                    display="flex"
                    alignItems="center"
                    gap={2}
                    width="100%"
                    minWidth={0}
                  >
                    <Typography variant="body2" noWrap flex={1} minWidth={0}>
                      {item.name}
                    </Typography>

                    {item.folderPath ? (
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        noWrap
                        sx={{ maxWidth: "55%" }}
                      >
                        {item.folderPath}
                      </Typography>
                    ) : null}
                  </Box>
                </ListItemButton>
              ))}
            </List>
          </Box>
        </Collapse>

        <Box px={2} pb={1}>
          <AudioPlayer
            currentFileId={currentFileId}
            currentFileName={currentFileName}
            playlist={playlist}
            onTrackChange={setCurrentTrack}
            shuffleEnabled={shuffleEnabled}
            onToggleShuffle={toggleShuffle}
            shuffleLabel={
              shuffleEnabled
                ? t("audioPlayer:actions.disableShuffle")
                : t("audioPlayer:actions.enableShuffle")
            }
            onListen={lyricsListenEnabled ? handleListen : undefined}
            listenIntervalMs={lyricsListenEnabled ? 250 : undefined}
          />
        </Box>
      </Paper>
    </Snackbar>
  );
};
