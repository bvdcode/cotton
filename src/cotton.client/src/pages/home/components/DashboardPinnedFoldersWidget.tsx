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

const PINNED_FOLDER_HEIGHT = 52;
const PINNED_FOLDER_ICON_SIZE = 40;

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
        direction="row"
        alignItems="center"
        justifyContent="center"
        textAlign="center"
        minHeight={PINNED_FOLDER_HEIGHT}
        gap={1}
      >
        <PushPin color="action" />
        <Box minWidth={0}>
          <Typography variant="body2" color="text.secondary">
            {t("dashboard.pinnedFolders.empty")}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {t("dashboard.pinnedFolders.emptyHint")}
          </Typography>
        </Box>
      </Stack>
    );
  }

  if (isPending && folders.length === 0) {
    return <Skeleton variant="rounded" height={PINNED_FOLDER_HEIGHT} />;
  }

  if (isError && folders.length === 0) {
    return (
      <Box minHeight={PINNED_FOLDER_HEIGHT} display="flex" alignItems="center">
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
    <Box
      display="grid"
      gridTemplateColumns="repeat(auto-fill, minmax(min(100%, 240px), 1fr))"
      gap={1}
    >
      {folders.map((folder) => (
        <Box key={folder.id} position="relative" minWidth={0}>
          <CardActionArea
            onClick={() => navigate(`/files/${folder.id}`)}
            sx={{
              border: "1px solid",
              borderColor: "divider",
              borderRadius: 1,
              height: "100%",
              minWidth: 0,
              width: "100%",
            }}
          >
            <Stack
              direction="row"
              alignItems="center"
              gap={1.5}
              px={1}
              py={0.75}
              pr={5}
            >
              <Box
                width={PINNED_FOLDER_ICON_SIZE}
                height={PINNED_FOLDER_ICON_SIZE}
                flexShrink={0}
                display="flex"
                alignItems="center"
                justifyContent="center"
              >
                <Folder color="primary" sx={{ fontSize: 28 }} />
              </Box>
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
            sx={{
              position: "absolute",
              top: "50%",
              right: 8,
              transform: "translateY(-50%)",
            }}
          >
            <Close fontSize="small" />
          </IconButton>
        </Box>
      ))}
    </Box>
  );
};
