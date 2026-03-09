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
import { Close, QueueMusic, Subtitles, TravelExplore } from "@mui/icons-material";
import type { SnackbarCloseReason } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
  selectAudioPlayerCurrentFileId,
  selectAudioPlayerCurrentFileName,
  selectAudioPlayerIsScanning,
  selectAudioPlayerLyricsLines,
  selectAudioPlayerLyricsOpen,
  selectAudioPlayerLyricsStatus,
  selectAudioPlayerOpen,
  selectAudioPlayerPlaylist,
  selectAudioPlayerShuffleEnabled,
  useAudioPlayerStore,
} from "../../shared/store/audioPlayerStore";
import { AudioPlayer } from "../../shared/ui/AudioPlayer";
import { AudioLyricsView } from "../../shared/ui/AudioLyricsView";
import { findActiveLrcLineIndex } from "../../shared/utils/lrc";

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
  const lyricsOpen = useAudioPlayerStore(selectAudioPlayerLyricsOpen);
  const lyricsStatus = useAudioPlayerStore(selectAudioPlayerLyricsStatus);
  const lyricsLines = useAudioPlayerStore(selectAudioPlayerLyricsLines);

  const close = useAudioPlayerStore((s) => s.close);
  const scanRecursively = useAudioPlayerStore((s) => s.scanRecursively);
  const setCurrentTrack = useAudioPlayerStore((s) => s.setCurrentTrack);
  const toggleShuffle = useAudioPlayerStore((s) => s.toggleShuffle);
  const toggleLyricsOpen = useAudioPlayerStore((s) => s.toggleLyricsOpen);
  const loadLyricsForTrack = useAudioPlayerStore((s) => s.loadLyricsForTrack);

  const [queueOpen, setQueueOpen] = React.useState<boolean>(false);
  const [lyricsActiveIndex, setLyricsActiveIndex] = React.useState<number>(0);
  const [lyricsCountdown, setLyricsCountdown] = React.useState<number | null>(
    null,
  );
  const [lyricsStarted, setLyricsStarted] = React.useState<boolean>(false);
  const countdownConsumedRef = React.useRef<boolean>(false);

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

  const [coverFailed, setCoverFailed] = React.useState(false);
  React.useEffect(() => {
    setCoverFailed(false);
  }, [currentPreviewUrl]);

  React.useEffect(() => {
    setLyricsActiveIndex(0);
    setLyricsCountdown(null);
    setLyricsStarted(false);
    countdownConsumedRef.current = false;
  }, [lyricsLines]);

  React.useEffect(() => {
    setLyricsCountdown(null);
    setLyricsStarted(false);
    countdownConsumedRef.current = false;
  }, [currentFileId]);

  React.useEffect(() => {
    if (!lyricsOpen) {
      return;
    }

    void loadLyricsForTrack({
      folderNodeId: currentItem?.nodeId ?? null,
      audioFileName: currentItem?.name ?? currentFileName,
    });
  }, [lyricsOpen, currentItem?.nodeId, currentItem?.name, currentFileName, loadLyricsForTrack]);

  const lyricsListenEnabled = lyricsOpen && lyricsLines.length > 0;

  const handleListen = React.useCallback(
    (timeSeconds: number) => {
      if (!lyricsListenEnabled) return;

      const firstTime = lyricsLines[0]?.timeSeconds;
      if (typeof firstTime !== "number") {
        return;
      }

      const started = timeSeconds >= firstTime;
      setLyricsStarted((prev) => (prev === started ? prev : started));

      if (started) {
        countdownConsumedRef.current = true;
        setLyricsCountdown((prev) => (prev === null ? prev : null));

        const next = findActiveLrcLineIndex(lyricsLines, timeSeconds);
        setLyricsActiveIndex((prev) => (prev === next ? prev : next));
        return;
      }

      if (countdownConsumedRef.current) {
        setLyricsCountdown((prev) => (prev === null ? prev : null));
        return;
      }

      const delta = firstTime - timeSeconds;

      if (delta > 3) {
        setLyricsCountdown((prev) => (prev === null ? prev : null));
        return;
      }

      const safeDelta = Math.max(0.0001, delta);
      const nextCountdown = Math.ceil(safeDelta);
      setLyricsCountdown((prev) => (prev === nextCountdown ? prev : nextCountdown));
    },
    [lyricsLines, lyricsListenEnabled],
  );

  const positionLabel =
    playlistTotal > 1 ? `${currentIndex + 1}/${playlistTotal}` : null;

  const handleClose = (
    _: Event | React.SyntheticEvent<Element, Event>,
    reason?: SnackbarCloseReason,
  ) => {
    if (reason === "clickaway") return;
    close();
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
                onError={() => setCoverFailed(true)}
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
              <IconButton size="small" onClick={() => close()} aria-label={t("audioPlayer:actions.close")}>
                <Close fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
        </Box>

        <Collapse in={lyricsOpen} timeout="auto" unmountOnExit>
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
                  activeIndex={lyricsStarted ? lyricsActiveIndex : 0}
                  isActivePlaying={lyricsStarted}
                />

                {lyricsCountdown !== null ? (
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
                      {lyricsCountdown}
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
