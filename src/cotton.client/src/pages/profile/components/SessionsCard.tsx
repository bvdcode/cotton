import {
  Box,
  Stack,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Typography,
  Alert,
  CircularProgress,
  Divider,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { sessionsApi, type SessionDto } from "../../../shared/api/sessionsApi";
import { SessionItem } from "./SessionItem";
import { confirm } from "material-ui-confirm";

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
            err instanceof Error ? err.message : "Failed to revoke session",
          );
        } finally {
          setRevoking(null);
        }
      }
    });
  };

  return (
    <Accordion
      disableGutters
      sx={(theme) => ({
        bgcolor: "background.paper",
        borderRadius: 2,
        boxShadow: theme.shadows[1],
        border: `1px solid ${theme.palette.divider}`,
        overflow: "hidden",
      })}
    >
      <AccordionSummary
        expandIcon={<ExpandMoreIcon />}
        aria-controls="sessions-content"
        id="sessions-header"
        sx={{
          minHeight: { xs: 56, sm: 64 },
          px: { xs: 2, sm: 3 },
          py: { xs: 1.25, sm: 1.5 },
          "& .MuiAccordionSummary-content": {
            margin: 0,
            gap: 1,
            alignItems: "center",
          },
          "& .MuiAccordionSummary-expandIconWrapper": {
            color: "text.secondary",
          },
        }}
      >
        <Box
          sx={{ display: "flex", alignItems: "center", width: "100%", gap: 2 }}
        >
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="h6" fontWeight={600} noWrap>
              {t("sessions.title")}
            </Typography>
            <Typography
              variant="body2"
              color="text.secondary"
              noWrap
              sx={{ mt: 0.5, fontSize: "0.9rem" }}
            >
              {t("sessions.description")}
            </Typography>
          </Box>

          <Box sx={{ display: "flex", alignItems: "center", gap: 2, ml: 1 }}>
            <Typography
              variant="body2"
              color="text.secondary"
              sx={{ whiteSpace: "nowrap" }}
            >
              {sessions.length}
            </Typography>
          </Box>
        </Box>
      </AccordionSummary>

      <Divider sx={{ mx: { xs: 0, sm: 3 } }} />

      <AccordionDetails sx={{ px: { xs: 2, sm: 3 }, pb: { xs: 2, sm: 3 } }}>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : error ? (
          <Alert severity="error" onClose={() => setError(null)} sx={{ mt: 2 }}>
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
          <Stack spacing={1.5} sx={{ mt: 2 }}>
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
      </AccordionDetails>
    </Accordion>
  );
};
