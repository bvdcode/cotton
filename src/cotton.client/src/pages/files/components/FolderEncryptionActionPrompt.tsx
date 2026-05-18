import { LockOpen, WarningAmber } from "@mui/icons-material";
import { Box, Button, Fade, Paper, Stack, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";

type FolderEncryptionPromptSeverity = "info" | "warning";

interface FolderEncryptionActionPromptProps {
  action: string;
  disabled?: boolean;
  message: string;
  onAction: () => void;
  severity: FolderEncryptionPromptSeverity;
}

export const FolderEncryptionActionPrompt = ({
  action,
  disabled = false,
  message,
  onAction,
  severity,
}: FolderEncryptionActionPromptProps) => {
  const color = severity === "warning" ? "warning" : "secondary";
  const Icon = severity === "warning" ? WarningAmber : LockOpen;

  return (
    <Fade in timeout={180}>
      <Box
        sx={{
          position: "fixed",
          left: 16,
          right: { xs: 16, sm: "auto" },
          bottom: {
            xs: "calc(16px + var(--audio-player-bar-offset, 0px))",
            sm: 16,
          },
          zIndex: (theme) => theme.zIndex.snackbar - 1,
          width: { xs: "auto", sm: 380 },
          pointerEvents: "none",
        }}
      >
        <Paper
          elevation={8}
          role="status"
          aria-live="polite"
          sx={(theme) => ({
            borderRadius: 2,
            border: `1px solid ${alpha(theme.palette[color].main, 0.45)}`,
            bgcolor: alpha(theme.palette.background.paper, 0.96),
            overflow: "hidden",
            pointerEvents: "auto",
          })}
        >
          <Stack
            direction="row"
            spacing={1.5}
            alignItems="center"
            sx={{ px: 2, py: 1.25 }}
          >
            <Box
              sx={(theme) => ({
                alignItems: "center",
                bgcolor: alpha(theme.palette[color].main, 0.14),
                borderRadius: 1,
                color: `${color}.main`,
                display: "flex",
                flex: "0 0 auto",
                height: 32,
                justifyContent: "center",
                width: 32,
              })}
            >
              <Icon fontSize="small" />
            </Box>
            <Typography
              variant="body2"
              sx={{
                flex: 1,
                minWidth: 0,
                overflowWrap: "anywhere",
              }}
            >
              {message}
            </Typography>
            <Button
              size="small"
              color={color}
              variant="contained"
              disabled={disabled}
              onClick={onAction}
              sx={{ flex: "0 0 auto" }}
            >
              {action}
            </Button>
          </Stack>
        </Paper>
      </Box>
    </Fade>
  );
};
