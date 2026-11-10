import {
  DeleteOutlined,
  EditOutlined,
  FolderOutlined,
  PlayArrow,
  ScheduleOutlined,
} from "@mui/icons-material";
import {
  Alert,
  Chip,
  Divider,
  IconButton,
  LinearProgress,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { RoutineRunStatus, type Routine } from "@shared/api/routinesApi";
import { useDecryptedNode } from "@shared/crypto/useDecryptedNodes";
import { RoutineHistory } from "./RoutineHistory";

interface Props {
  routine: Routine;
  pending: boolean;
  onRun: () => void;
  onEdit: () => void;
  onDelete: () => void;
}
export const RoutineCard = ({
  routine,
  pending,
  onRun,
  onEdit,
  onDelete,
}: Props) => {
  const { t, i18n } = useTranslation("routines");
  const folder = useDecryptedNode(routine.targetNode);
  const dateFieldLabel = () => {
    switch (routine.settings.dateField) {
      case 0:
        return t("createdAt");
      case 1:
        return t("updatedAt");
    }
  };
  const running = routine.latestRun?.status === RoutineRunStatus.Running;
  return (
    <Paper
      variant="outlined"
      sx={{ overflow: "hidden", background: "transparent" }}
    >
      <Stack spacing={1} sx={{ p: 2 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ alignItems: { sm: "flex-start" } }}
        >
          <Stack spacing={1} sx={{ flex: 1, minWidth: 0 }}>
            <Typography
              component="h2"
              variant="subtitle1"
              sx={{ fontWeight: 600, overflowWrap: "anywhere" }}
            >
              {routine.name}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("condition", {
                days: routine.settings.olderThanDays,
                field: dateFieldLabel(),
              })}
            </Typography>
            <Stack
              direction="row"
              spacing={1.5}
              useFlexGap
              sx={{ alignItems: "center", flexWrap: "wrap", minWidth: 0 }}
            >
              <Tooltip
                title={folder?.name ?? t("errors.WorkingFolderUnavailable")}
              >
                <Stack
                  direction="row"
                  spacing={1}
                  sx={{
                    alignItems: "center",
                    minWidth: 0,
                  }}
                >
                  <FolderOutlined fontSize="small" color="action" />
                  <Typography
                    variant="body2"
                    sx={{ minWidth: 0, overflowWrap: "anywhere" }}
                  >
                    {folder?.name ?? t("errors.WorkingFolderUnavailable")}
                  </Typography>
                </Stack>
              </Tooltip>
              {routine.enabled && routine.nextExecutionAt && (
                <Tooltip
                  title={t("nextRun", {
                    date: new Intl.DateTimeFormat(i18n.language, {
                      dateStyle: "medium",
                      timeStyle: "short",
                    }).format(new Date(routine.nextExecutionAt)),
                  })}
                >
                  <Stack
                    direction="row"
                    spacing={1}
                    sx={{
                      alignItems: "center",
                      minWidth: 0,
                    }}
                  >
                    <ScheduleOutlined fontSize="small" color="action" />
                    <Typography
                      component="time"
                      dateTime={routine.nextExecutionAt}
                      variant="body2"
                      color="text.secondary"
                    >
                      {new Intl.DateTimeFormat(i18n.language, {
                        dateStyle: "medium",
                        timeStyle: "short",
                      }).format(new Date(routine.nextExecutionAt))}
                    </Typography>
                  </Stack>
                </Tooltip>
              )}
            </Stack>
          </Stack>
          <Stack
            direction="row"
            spacing={0.5}
            sx={{
              alignItems: "center",
              alignSelf: { xs: "flex-end", sm: "flex-start" },
              flexShrink: 0,
              minWidth: 0,
              maxWidth: "100%",
              "& > span": { flexShrink: 0 },
            }}
          >
            <Chip
              size="small"
              label={routine.enabled ? t("enabled") : t("paused")}
              sx={{ minWidth: 0 }}
            />
            <Tooltip title={t("runNow")}>
              <span>
                <IconButton
                  aria-label={t("runNow")}
                  disabled={pending || running || !routine.enabled}
                  onClick={onRun}
                >
                  <PlayArrow />
                </IconButton>
              </span>
            </Tooltip>
            <Tooltip title={t("editTitle")}>
              <span>
                <IconButton
                  aria-label={t("editTitle")}
                  disabled={pending || running}
                  onClick={onEdit}
                >
                  <EditOutlined />
                </IconButton>
              </span>
            </Tooltip>
            <Tooltip title={t("deleteRoutine")}>
              <span>
                <IconButton
                  aria-label={t("deleteRoutine")}
                  disabled={pending || running}
                  onClick={onDelete}
                >
                  <DeleteOutlined />
                </IconButton>
              </span>
            </Tooltip>
          </Stack>
        </Stack>
        {running && routine.latestRun && (
          <Stack spacing={1}>
            <LinearProgress />
            <Typography variant="body2" color="text.secondary">
              {t("counts", {
                processed: routine.latestRun.processedCount,
                deleted: routine.latestRun.deletedCount,
              })}
            </Typography>
          </Stack>
        )}
        {routine.latestRun?.error && (
          <Alert severity="error">
            {t(`errors.${routine.latestRun.error}`)}
          </Alert>
        )}
      </Stack>
      <Divider />
      <RoutineHistory id={routine.id} latestRun={routine.latestRun} />
    </Paper>
  );
};
