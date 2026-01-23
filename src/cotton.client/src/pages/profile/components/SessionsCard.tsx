import {
  Box,
  Paper,
  Stack,
  Typography,
  Alert,
  CircularProgress,
  Divider,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { sessionsApi, type SessionDto } from "../../../shared/api/sessionsApi";
import { SessionItem } from "./SessionItem";

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
      // Sort by lastSeenAt descending (most recent first)
      const sorted = data.sort(
        (a, b) =>
          new Date(b.lastSeenAt).getTime() - new Date(a.lastSeenAt).getTime(),
      );
      setSessions(sorted);
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
      sx={{
        p: {
          xs: 2,
          sm: 3,
        },
      }}
    >
      <Stack spacing={2}>
        <Box>
          <Typography variant="h6" fontWeight={600} gutterBottom>
            {t("sessions.title", "Active Sessions")}
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
              <SessionItem
                key={session.sessionId}
                session={session}
                isRevoking={revoking === session.sessionId}
                onRevoke={handleRevokeSession}
              />
            ))}
          </Stack>
        )}
      </Stack>
    </Paper>
  );
};
