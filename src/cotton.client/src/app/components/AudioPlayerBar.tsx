import React from "react";
import {
  Box,
  Collapse,
  CircularProgress,
  Divider,
  IconButton,
  List,
  ListItemButton,
  Paper,
  Snackbar,
  Tooltip,
  Typography,
} from "@mui/material";
import { Close, QueueMusic, TravelExplore } from "@mui/icons-material";
import type { SnackbarCloseReason } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
  selectAudioPlayerCurrentFileId,
  selectAudioPlayerCurrentFileName,
  selectAudioPlayerIsScanning,
  selectAudioPlayerOpen,
  selectAudioPlayerPlaylist,
  useAudioPlayerStore,
} from "../../shared/store/audioPlayerStore";
import { AudioPlayer } from "../../shared/ui/AudioPlayer";

export const AudioPlayerBar: React.FC = () => {
  const { t } = useTranslation(["audioPlayer"]);

  const open = useAudioPlayerStore(selectAudioPlayerOpen);
  const isScanning = useAudioPlayerStore(selectAudioPlayerIsScanning);
  const playlist = useAudioPlayerStore(selectAudioPlayerPlaylist);
  const currentFileId = useAudioPlayerStore(selectAudioPlayerCurrentFileId);
  const currentFileName = useAudioPlayerStore(selectAudioPlayerCurrentFileName);

  const close = useAudioPlayerStore((s) => s.close);
  const scanRecursively = useAudioPlayerStore((s) => s.scanRecursively);
  const setCurrentTrack = useAudioPlayerStore((s) => s.setCurrentTrack);

  const [queueOpen, setQueueOpen] = React.useState<boolean>(false);

  const playlistTotal = playlist.length;
  const currentIndex = React.useMemo<number>(() => {
    const index = playlist.findIndex((x) => x.id === currentFileId);
    return index >= 0 ? index : 0;
  }, [playlist, currentFileId]);

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
        left: 0,
        right: 0,
        transform: "none",
        display: "flex",
        justifyContent: "center",
        px: { xs: 0, sm: 2 },
        pb: "calc(env(safe-area-inset-bottom, 0px) + 8px)",
        "&.MuiSnackbar-anchorOriginBottomCenter": {
          left: 0,
          right: 0,
          transform: "none",
        },
      }}
    >
      <Paper
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
          <Box display="flex" alignItems="baseline" gap={1} flex={1} minWidth={0}>
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
          />
        </Box>
      </Paper>
    </Snackbar>
  );
};
