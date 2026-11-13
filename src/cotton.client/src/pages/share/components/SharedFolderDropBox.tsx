import { CheckCircleOutlined, CloudUploadOutlined } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Fade,
  LinearProgress,
  Stack,
  Typography,
  useMediaQuery,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { SharedNodeInfoDto } from "@shared/api/sharedFoldersApi";
import { useSharedFolderUpload } from "../hooks/useSharedFolderUpload";
import { useSharedUploadBatch } from "../hooks/useSharedUploadBatch";
import { DraggingOverlay } from "../../files/components/DraggingOverlay";

const uploadCompleted = () => {};

export const SharedFolderDropBox = ({ info }: { info: SharedNodeInfoDto }) => {
  const { t } = useTranslation("share");
  const reducedMotion = useMediaQuery("(prefers-reduced-motion: reduce)");
  const batch = useSharedUploadBatch();
  const upload = useSharedFolderUpload(
    info,
    info.nodeId,
    info.name,
    uploadCompleted,
    batch.onQueued,
  );
  const succeeded = batch.finished && batch.failed === 0;
  const state = succeeded
    ? "success"
    : batch.tasks.length > 0
      ? "progress"
      : "ready";

  return (
    <Box
      component="main"
      sx={{
        flex: 1,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        p: { xs: 2, sm: 3 },
      }}
      {...upload.dragHandlers}
    >
      <DraggingOverlay
        open={upload.isDragging}
        {...upload.dragHandlers}
        label={t("actions.dropFiles", { ns: "files" })}
      />
      <Fade in appear key={state} timeout={reducedMotion ? 0 : undefined}>
        <Stack
          spacing={3}
          sx={{
            width: "100%",
            maxWidth: 560,
            py: 3,
            alignItems: "center",
            textAlign: "center",
          }}
        >
          {succeeded ? (
            <CheckCircleOutlined color="success" sx={{ fontSize: 56 }} />
          ) : (
            <CloudUploadOutlined color="primary" sx={{ fontSize: 56 }} />
          )}
          <Stack spacing={1}>
            <Typography variant="h6">{t(`dropBox.${state}`)}</Typography>
            {succeeded && (
              <Typography color="text.secondary">
                {t("dropBox.thanks")}
              </Typography>
            )}
          </Stack>
          {batch.tasks.length > 0 && !succeeded && (
            <Stack
              spacing={2}
              sx={{ width: "100%", textAlign: "left" }}
              aria-live="polite"
            >
              {batch.tasks.length > 1 && (
                <>
                  <Typography variant="body2">
                    {t("dropBox.uploaded", {
                      completed: batch.completed,
                      total: batch.tasks.length,
                    })}
                  </Typography>
                  <LinearProgress
                    variant="determinate"
                    value={batch.progress * 100}
                  />
                </>
              )}
              <Stack
                spacing={1.5}
                sx={{ maxHeight: "18rem", overflow: "auto" }}
              >
                {batch.tasks.map((task) => (
                  <Stack key={task.id} spacing={0.5}>
                    <Typography variant="body2" noWrap title={task.label}>
                      {task.label}
                    </Typography>
                    {task.status === "failed" ? (
                      <Alert severity="error">{t("dropBox.failed")}</Alert>
                    ) : (
                      <LinearProgress
                        variant="determinate"
                        value={task.progress01 * 100}
                      />
                    )}
                  </Stack>
                ))}
              </Stack>
            </Stack>
          )}
          {batch.finished ? (
            <Button variant="contained" onClick={batch.reset}>
              {t("dropBox.more")}
            </Button>
          ) : (
            <Button
              variant={batch.tasks.length > 0 ? "outlined" : "contained"}
              startIcon={<CloudUploadOutlined />}
              onClick={upload.openPicker}
            >
              {t(batch.tasks.length > 0 ? "dropBox.add" : "dropBox.select")}
            </Button>
          )}
        </Stack>
      </Fade>
    </Box>
  );
};
