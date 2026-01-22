import {
  Box,
  Paper,
  Stack,
  Typography,
  IconButton,
  Tooltip,
  Divider,
  Alert,
  CircularProgress,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import LogoutIcon from "@mui/icons-material/Logout";
import DevicesIcon from "@mui/icons-material/Devices";
import { sessionsApi, type SessionDto } from "../../../shared/api/sessionsApi";

const formatLocation = (session: SessionDto): string => {
  const parts = [session.city, session.region, session.country].filter(Boolean);
  return parts.join(", ") || "Unknown location";
};

const formatDuration = (duration: string): string => {
  // Parse C# TimeSpan format (e.g., "01:23:45" or "1.02:03:04")
  const parts = duration.split(".");
  const timePart = parts.length > 1 ? parts[1] : parts[0];
  const [hours, minutes] = timePart.split(":");

  const totalHours =
    parts.length > 1
      ? parseInt(parts[0]) * 24 + parseInt(hours)
      : parseInt(hours);

  if (totalHours < 1) {
    return `${parseInt(minutes)}m`;
  } else if (totalHours < 24) {
    return `${totalHours}h`;
  } else {
    const days = Math.floor(totalHours / 24);
    return `${days}d`;
  }
};

export const SessionsCard = () => {
  const { t } = useTranslation("profile");
  const [sessions, setSessions] = useState<SessionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revoking, setRevoking] = useState<string | null>(null);

  const loadSessions = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await sessionsApi.getSessions();
      setSessions(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load sessions");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSessions();
  }, []);

  const handleRevokeSession = async (sessionId: string) => {
    try {
      setRevoking(sessionId);
      await sessionsApi.revokeSession(sessionId);
      // Remove the session from the list
      setSessions((prev) => prev.filter((s) => s.sessionId !== sessionId));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to revoke session");
    } finally {
      setRevoking(null);
    }
  };

  return (
    <Paper
      elevation={0}
      sx={{
        p: { xs: 2, sm: 3 },
        borderRadius: 2,
        border: (theme) => `1px solid ${theme.palette.divider}`,
      }}
    >
      <Stack spacing={2}>
        <Box>
          <Typography variant="h6" fontWeight={600} gutterBottom>
            {t("sessions.title", "Active Sessions")}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t(
              "sessions.description",
              "Manage your active sessions across different devices",
            )}
          </Typography>
        </Box>

        <Divider />

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : error ? (
          <Alert severity="error" onClose={() => setError(null)}>
            {error}
          </Alert>
        ) : sessions.length === 0 ? (
          <Typography
            color="text.secondary"
            sx={{ py: 2, textAlign: "center" }}
          >
            {t("sessions.noActiveSessions", "No active sessions")}
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {sessions.map((session) => (
              <Paper
                key={session.sessionId}
                variant="outlined"
                sx={{
                  p: 2,
                  display: "flex",
                  alignItems: "center",
                  gap: 2,
                  transition: "background-color 0.2s",
                  "&:hover": {
                    bgcolor: "action.hover",
                  },
                }}
              >
                <DevicesIcon sx={{ color: "text.secondary", fontSize: 28 }} />

                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Tooltip
                    title={session.userAgent || ""}
                    placement="right"
                    arrow
                  >
                    <Typography
                      variant="body1"
                      fontWeight={500}
                      sx={{
                        cursor: "help",
                        width: "fit-content",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                        maxWidth: "100%",
                      }}
                    >
                      {session.device || session.userAgent || "Unknown device"}
                    </Typography>
                  </Tooltip>

                  <Stack
                    direction="row"
                    spacing={1.5}
                    sx={{ mt: 0.5 }}
                    divider={
                      <Typography variant="caption" color="text.secondary">
                        •
                      </Typography>
                    }
                  >
                    <Typography variant="caption" color="text.secondary">
                      {formatLocation(session)}
                    </Typography>
                    {session.ipAddress && (
                      <Typography variant="caption" color="text.secondary">
                        {session.ipAddress}
                      </Typography>
                    )}
                    <Typography variant="caption" color="text.secondary">
                      {formatDuration(session.totalSessionDuration)}
                    </Typography>
                  </Stack>
                </Box>

                <Tooltip
                  title={t("sessions.revokeSession", "End session")}
                  placement="left"
                >
                  <span>
                    <IconButton
                      size="small"
                      color="error"
                      onClick={() => handleRevokeSession(session.sessionId)}
                      disabled={revoking === session.sessionId}
                      sx={{
                        "&:hover": {
                          bgcolor: "error.main",
                          color: "error.contrastText",
                        },
                      }}
                    >
                      {revoking === session.sessionId ? (
                        <CircularProgress size={20} color="inherit" />
                      ) : (
                        <LogoutIcon fontSize="small" />
                      )}
                    </IconButton>
                  </span>
                </Tooltip>
              </Paper>
            ))}
          </Stack>
        )}
      </Stack>
    </Paper>
  );
};
