import { Close, Folder, PushPin } from "@mui/icons-material";
import {
  Box,
  CardActionArea,
  IconButton,
  Skeleton,
  Stack,
  Typography,
} from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import { DashboardQueryError } from "./DashboardQueryError";

const PINNED_FOLDERS_STATE_HEIGHT = 72;

interface DashboardPinnedFoldersWidgetProps {
  folderIds: readonly string[];
  folders: readonly NodeDto[];
  isError: boolean;
  isPending: boolean;
  onRetry: () => void;
  onUnpin: (folderId: string) => void;
}

export const DashboardPinnedFoldersWidget = ({
  folderIds,
  folders,
  isError,
  isPending,
  onRetry,
  onUnpin,
}: DashboardPinnedFoldersWidgetProps) => {
  const { t } = useTranslation("home");
  const navigate = useNavigate();

  if (folderIds.length === 0) {
    return (
      <Stack
        alignItems="center"
        justifyContent="center"
        textAlign="center"
        height={PINNED_FOLDERS_STATE_HEIGHT}
        gap={0.25}
      >
        <PushPin color="action" />
        <Typography variant="body2" color="text.secondary">
          {t("dashboard.pinnedFolders.empty")}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {t("dashboard.pinnedFolders.emptyHint")}
        </Typography>
      </Stack>
    );
  }

  if (isPending && folders.length === 0) {
    return <Skeleton variant="rounded" height={PINNED_FOLDERS_STATE_HEIGHT} />;
  }

  if (isError && folders.length === 0) {
    return (
      <Box
        height={PINNED_FOLDERS_STATE_HEIGHT}
        display="flex"
        alignItems="center"
      >
        <Box width="100%">
          <DashboardQueryError
            message={t("dashboard.pinnedFolders.loadFailed")}
            onRetry={onRetry}
          />
        </Box>
      </Box>
    );
  }

  return (
    <Stack direction="row" flexWrap="wrap" gap={1}>
      {folders.map((folder) => (
        <Box
          key={folder.id}
          position="relative"
          minWidth={0}
          width={{ xs: "100%", sm: 280 }}
        >
          <CardActionArea
            onClick={() => navigate(`/files/${folder.id}`)}
            sx={{ borderRadius: 1, minWidth: 0, width: "100%" }}
          >
            <Stack direction="row" alignItems="center" gap={1.25} p={1} pr={5}>
              <Folder color="primary" />
              <Box minWidth={0}>
                <Typography variant="body2" noWrap>
                  {folder.name}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {t("dashboard.pinnedFolders.folder")}
                </Typography>
              </Box>
            </Stack>
          </CardActionArea>
          <IconButton
            size="small"
            aria-label={t("dashboard.pinnedFolders.unpin")}
            onClick={() => onUnpin(folder.id)}
            sx={{ position: "absolute", top: 8, right: 8 }}
          >
            <Close fontSize="small" />
          </IconButton>
        </Box>
      ))}
    </Stack>
  );
};
