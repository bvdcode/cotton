import React from "react";
import {
  Box,
  Collapse,
  Divider,
  IconButton,
  LinearProgress,
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
import { AudioPlayerQueue } from "./AudioPlayerQueue";
import { AudioPlayer } from "../../shared/ui/AudioPlayer";
import { AudioLyricsView } from "../../shared/ui/AudioLyricsView";
import { useAudioLyricsPlayback } from "./useAudioLyricsPlayback";
import { useAudioPlayerBarOffset } from "./useAudioPlayerBarOffset";
import {
  getAudioDisplaySubtitle,
  getAudioDisplayTitle,
} from "../../shared/utils/mediaMetadata";

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

  const queueLabel = t(
    queueOpen
      ? "audioPlayer:actions.hideQueue"
      : "audioPlayer:actions.showQueue",
  );
  const lyricsLabel = t(
    lyricsOpen
      ? "audioPlayer:actions.hideLyrics"
      : "audioPlayer:actions.showLyrics",
  );

  const paperRef = useAudioPlayerBarOffset(open);

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
  const currentDisplayTitle = currentItem
    ? getAudioDisplayTitle(currentItem)
    : currentFileName;
  const currentDisplaySubtitle = currentItem
    ? getAudioDisplaySubtitle(currentItem)
    : undefined;

  const [failedCoverPreviewUrl, setFailedCoverPreviewUrl] = React.useState<
    string | null
  >(null);
  const coverFailed =
    currentPreviewUrl !== undefined &&
    failedCoverPreviewUrl === currentPreviewUrl;
  const effectiveLyricsOpen = open && lyricsOpen;

  const {
    lyricsLines,
    lyricsStatus,
    lyricsPlayback,
    lyricsListenEnabled,
    handleListen,
  } = useAudioLyricsPlayback({
    currentFileId,
    currentFileName,
    currentItem,
    effectiveLyricsOpen,
  });

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
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1,
            px: 2,
            pt: 1,
          }}
        >
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 1,
              flex: 1,
              minWidth: 0,
            }}
          >
            {currentPreviewUrl && !coverFailed ? (
              <Box
                component="img"
                src={currentPreviewUrl}
                alt=""
                onError={() => setFailedCoverPreviewUrl(currentPreviewUrl)}
                sx={{
                  width: 28,
                  height: 28,
                  borderRadius: 0.5,
                  flexShrink: 0,
                  objectFit: "cover",
                }}
              />
            ) : null}

            <Box
              sx={{
                display: "flex",
                flexDirection: "column",
                flex: 1,
                minWidth: 0,
              }}
            >
              <Typography variant="subtitle2" noWrap>
                {currentDisplayTitle}
              </Typography>
              {currentDisplaySubtitle ? (
                <Typography
                  variant="caption"
                  noWrap
                  sx={{
                    color: "text.secondary",
                  }}
                >
                  {currentDisplaySubtitle}
                </Typography>
              ) : null}
            </Box>

            {positionLabel && (
              <Typography
                variant="caption"
                noWrap
                sx={{
                  color: "text.secondary",
                }}
              >
                {positionLabel}
              </Typography>
            )}
          </Box>

          <Tooltip title={queueLabel} arrow>
            <span>
              <IconButton
                size="small"
                onClick={() => setQueueOpen((prev) => !prev)}
                color={queueOpen ? "primary" : "default"}
                aria-label={queueLabel}
              >
                <QueueMusic fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>

          <Tooltip title={lyricsLabel} arrow>
            <span>
              <IconButton
                size="small"
                onClick={() => toggleLyricsOpen()}
                aria-label={lyricsLabel}
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
                loading={isScanning}
                aria-label={t("audioPlayer:actions.scanRecursively")}
              >
                <TravelExplore fontSize="small" />
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
          <Box
            sx={{
              px: 2,
              py: 1.25,
            }}
          >
            {!currentItem?.nodeId ? (
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                }}
              >
                {t("audioPlayer:lyrics.unavailable")}
              </Typography>
            ) : lyricsStatus === "loading" ? (
              <LinearProgress />
            ) : lyricsLines.length > 0 ? (
              <Box
                sx={{
                  position: "relative",
                  height: lyricsViewHeightPx,
                }}
              >
                <AudioLyricsView
                  lines={lyricsLines}
                  activeIndex={
                    lyricsPlayback.started ? lyricsPlayback.activeIndex : 0
                  }
                  isActivePlaying={lyricsPlayback.started}
                />

                {lyricsPlayback.countdown !== null ? (
                  <Box
                    sx={{
                      position: "absolute",
                      top: 0,
                      left: 0,
                      right: 0,
                      bottom: 0,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      pointerEvents: "none",
                    }}
                  >
                    <Typography
                      variant="h4"
                      sx={{
                        fontWeight: 800,
                        textAlign: "center",
                        opacity: 0.8,
                      }}
                    >
                      {lyricsPlayback.countdown}
                    </Typography>
                  </Box>
                ) : null}
              </Box>
            ) : lyricsStatus === "error" ? (
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                }}
              >
                {t("audioPlayer:lyrics.loadFailed")}
              </Typography>
            ) : (
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                }}
              >
                {t("audioPlayer:lyrics.notFound")}
              </Typography>
            )}
          </Box>
        </Collapse>

        <AudioPlayerQueue
          open={queueOpen}
          playlist={playlist}
          currentFileId={currentFileId}
          onSelect={(item) => {
            setCurrentTrack(item);
            setQueueOpen(false);
          }}
        />

        <Box
          sx={{
            px: 2,
            pb: 1,
          }}
        >
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
