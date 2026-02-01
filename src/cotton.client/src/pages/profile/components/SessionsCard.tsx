import { Box, Stack, Typography, Alert, CircularProgress } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useCallback, useEffect, useState } from "react";
import { sessionsApi, type SessionDto } from "../../../shared/api/sessionsApi";
import { SessionItem } from "./SessionItem";
import { confirm } from "material-ui-confirm";
import { Key } from "@mui/icons-material";
import { ProfileAccordionCard } from "./ProfileAccordionCard";

export const SessionsCard = () => {
  const { t } = useTranslation("profile");
  const [sessions, setSessions] = useState<SessionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revoking, setRevoking] = useState<string | null>(null);

  const loadSessions = useCallback(async () => {
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
      setError(
        err instanceof Error ? err.message : t("sessions.errors.loadFailed"),
      );
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadSessions();
  }, [loadSessions]);

  const handleRevokeSession = async (sessionId: string) => {
    confirm({
      title: t("sessions.revokeConfirmTitle"),
      description: t("sessions.revokeConfirmDescription"),
      confirmationText: t("sessions.revokeConfirmButton"),
      cancellationText: t("sessions.revokeCancelButton"),
    }).then(async (result) => {
      if (result.confirmed) {
        try {
          setRevoking(sessionId);
          await sessionsApi.revokeSession(sessionId);
          // Remove the session from the list
          setSessions((prev) => prev.filter((s) => s.sessionId !== sessionId));
        } catch (err) {
          setError(
            err instanceof Error
              ? err.message
              : t("sessions.errors.revokeFailed"),
          );
        } finally {
          setRevoking(null);
        }
      }
    });
  };

  return (
    <ProfileAccordionCard
      id="sessions-header"
      ariaControls="sessions-content"
      icon={<Key color="primary" />}
      title={t("sessions.title")}
      description={t("sessions.description")}
      count={sessions.length}
    >
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={32} />
        </Box>
      ) : error ? (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mt: 2 }}>
          {error}
        </Alert>
      ) : sessions.length === 0 ? (
        <Typography color="text.secondary" sx={{ py: 2, textAlign: "center" }}>
          {t("sessions.noActiveSessions")}
        </Typography>
      ) : (
        <Stack spacing={1.5} sx={{ mt: 2, mb: 1 }}>
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
    </ProfileAccordionCard>
  );
};
