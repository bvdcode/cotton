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
import { usePinnedFoldersQuery } from "../../../shared/api/queries/layouts";

interface DashboardPinnedFoldersWidgetProps {
  enabled: boolean;
  folderIds: readonly string[];
  onUnpin: (folderId: string) => void;
}

export const DashboardPinnedFoldersWidget = ({
  enabled,
  folderIds,
  onUnpin,
}: DashboardPinnedFoldersWidgetProps) => {
  const { t } = useTranslation("home");
  const navigate = useNavigate();
  const query = usePinnedFoldersQuery(folderIds, enabled);
  const folders = query.data ?? [];

  if (folderIds.length === 0) {
    return (
      <Stack alignItems="center" textAlign="center" py={2} gap={0.5}>
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

  if (query.isPending && folders.length === 0) {
    return <Skeleton variant="rounded" height={72} />;
  }

  return (
    <Box
      display="grid"
      gridTemplateColumns={{ xs: "1fr", sm: "repeat(2, minmax(0, 1fr))" }}
      gap={1}
    >
      {folders.map((folder) => (
        <Box key={folder.id} position="relative" minWidth={0}>
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
    </Box>
  );
};
