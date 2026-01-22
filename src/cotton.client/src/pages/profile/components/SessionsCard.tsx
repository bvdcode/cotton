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
  Chip,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import LogoutIcon from "@mui/icons-material/Logout";
import DevicesIcon from "@mui/icons-material/Devices";
import PhoneIphoneIcon from "@mui/icons-material/PhoneIphone";
import PhoneAndroidIcon from "@mui/icons-material/PhoneAndroid";
import TabletIcon from "@mui/icons-material/Tablet";
import ComputerIcon from "@mui/icons-material/Computer";
import LaptopIcon from "@mui/icons-material/Laptop";
import TvIcon from "@mui/icons-material/Tv";
import SportsEsportsIcon from "@mui/icons-material/SportsEsports";
import SmartToyIcon from "@mui/icons-material/SmartToy";
import CodeIcon from "@mui/icons-material/Code";
import DnsIcon from "@mui/icons-material/Dns";
import { sessionsApi, type SessionDto } from "../../../shared/api/sessionsApi";
import { formatTimeAgo } from "../../../shared/utils/formatTimeAgo";

const formatLocation = (session: SessionDto): string => {
  const parts = [session.city, session.region, session.country].filter(Boolean);
  return parts.join(", ") || "Unknown location";
};

const getDeviceIcon = (device: string) => {
  const deviceLower = device.toLowerCase();

  if (deviceLower.includes("iphone") || deviceLower.includes("ipod")) {
    return <PhoneIphoneIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("ipad")) {
    return <TabletIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("android phone")) {
    return <PhoneAndroidIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("android tablet")) {
    return <TabletIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (
    deviceLower.includes("windows pc") ||
    deviceLower.includes("mac") ||
    deviceLower.includes("linux pc")
  ) {
    return <ComputerIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("chromebook")) {
    return <LaptopIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("smart tv")) {
    return <TvIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("game console")) {
    return <SportsEsportsIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("bot")) {
    return <SmartToyIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("script")) {
    return <CodeIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("server")) {
    return <DnsIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("mobile")) {
    return <PhoneIphoneIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }

  return <DevicesIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
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
                  ...(session.isCurrentSession && {
                    borderColor: "primary.main",
                    borderWidth: 2,
                  }),
                }}
              >
                {getDeviceIcon(session.device)}

                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Stack
                    direction="row"
                    spacing={1}
                    alignItems="center"
                    sx={{ mb: 0.5 }}
                  >
                    <Tooltip title={session.userAgent || ""} arrow>
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
                        {session.device ||
                          session.userAgent ||
                          "Unknown device"}
                      </Typography>
                    </Tooltip>
                    {session.isCurrentSession && (
                      <Chip
                        label="Current"
                        size="small"
                        color="primary"
                        sx={{ height: 20, fontSize: "0.7rem" }}
                      />
                    )}
                  </Stack>

                  <Stack
                    direction={{ xs: "column", sm: "row" }}
                    spacing={{ xs: 0.25, sm: 1.5 }}
                    divider={
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ display: { xs: "none", sm: "block" } }}
                      >
                        •
                      </Typography>
                    }
                  >
                    <Typography variant="caption" color="text.secondary" noWrap>
                      {formatTimeAgo(session.lastSeenAt)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary" noWrap>
                      {formatLocation(session)}
                    </Typography>
                    {session.ipAddress && (
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                        }}
                      >
                        {session.ipAddress}
                      </Typography>
                    )}
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
