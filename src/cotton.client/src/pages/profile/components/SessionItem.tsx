import {
  Box,
  Stack,
  Typography,
  IconButton,
  Tooltip,
  CircularProgress,
  Chip,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import LogoutIcon from "@mui/icons-material/Logout";
import type { SessionDto } from "../../../shared/api/sessionsApi";
import { formatTimeAgo } from "../../../shared/utils/formatTimeAgo";
import { getDeviceIcon } from "./sessionUtils";
import { formatLocation, formatDuration } from "./sessionUtils";

interface SessionItemProps {
  session: SessionDto;
  isRevoking: boolean;
  onRevoke: (sessionId: string) => void;
}

export const SessionItem = ({
  session,
  isRevoking,
  onRevoke,
}: SessionItemProps) => {
  const { t } = useTranslation("profile");

  return (
    <Box
      sx={{
        py: 1.5,
        display: "flex",
        alignItems: "center",
        gap: 1,
      }}
    >
      {getDeviceIcon(session.device)}

      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
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
                t("sessions.unknownDevice")}
            </Typography>
          </Tooltip>
          {session.isCurrentSession && (
            <Chip
              label={t("sessions.currentSession")}
              size="small"
              color="primary"
              sx={{ height: 20, fontSize: "0.7rem" }}
            />
          )}
        </Stack>

        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={{ xs: 0.1, sm: 1.5 }}
        >
          <Typography variant="caption" color="text.secondary" noWrap>
            {formatTimeAgo(session.lastSeenAt, t)} |{" "}
            {t("sessions.elapsed", {
              duration: formatDuration(session.totalSessionDuration, t),
            })}
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

      <Tooltip title={t("sessions.revokeSession")} placement="left">
        <span>
          <IconButton
            size="small"
            color="error"
            onClick={() => onRevoke(session.sessionId)}
            disabled={isRevoking}
          >
            {isRevoking ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              <LogoutIcon fontSize="small" />
            )}
          </IconButton>
        </span>
      </Tooltip>
    </Box>
  );
};
