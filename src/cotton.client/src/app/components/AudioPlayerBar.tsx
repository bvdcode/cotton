import React from "react";
import {
  Box,
  CircularProgress,
  IconButton,
  Paper,
  Snackbar,
  Typography,
} from "@mui/material";
import { Close, TravelExplore } from "@mui/icons-material";
import type { SnackbarCloseReason } from "@mui/material";
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
  const open = useAudioPlayerStore(selectAudioPlayerOpen);
  const isScanning = useAudioPlayerStore(selectAudioPlayerIsScanning);
  const playlist = useAudioPlayerStore(selectAudioPlayerPlaylist);
  const currentFileId = useAudioPlayerStore(selectAudioPlayerCurrentFileId);
  const currentFileName = useAudioPlayerStore(selectAudioPlayerCurrentFileName);

  const close = useAudioPlayerStore((s) => s.close);
  const scanRecursively = useAudioPlayerStore((s) => s.scanRecursively);
  const setCurrentTrack = useAudioPlayerStore((s) => s.setCurrentTrack);

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
        width: "100%",
        px: { xs: 0, sm: 2 },
        pb: "calc(env(safe-area-inset-bottom, 0px) + 8px)",
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
          <Typography
            variant="subtitle2"
            sx={{ flex: 1, minWidth: 0 }}
            noWrap
          >
            {currentFileName}
          </Typography>

          <IconButton
            size="small"
            onClick={() => {
              void scanRecursively();
            }}
            disabled={isScanning}
          >
            {isScanning ? (
              <CircularProgress size={18} />
            ) : (
              <TravelExplore fontSize="small" />
            )}
          </IconButton>

          <IconButton size="small" onClick={() => close()}>
            <Close fontSize="small" />
          </IconButton>
        </Box>

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
