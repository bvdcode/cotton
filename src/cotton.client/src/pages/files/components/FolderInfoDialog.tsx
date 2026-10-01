import { useQuery } from "@tanstack/react-query";
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "@shared/api/layoutsApi";
import { nodesApi } from "@shared/api/nodesApi";
import { queryKeys } from "@shared/api/queries/queryKeys";
import { useAuthStore } from "@shared/store/authStore";
import { formatBytes } from "@shared/utils/formatBytes";

interface FolderInfoDialogProps {
  folder: NodeDto | null;
  onClose: () => void;
}

export const FolderInfoDialog = ({ folder, onClose }: FolderInfoDialogProps) => {
  const { t, i18n } = useTranslation(["files", "common"]);
  const dateFormatter = new Intl.DateTimeFormat(i18n.resolvedLanguage, {
    dateStyle: "medium",
    timeStyle: "short",
  });
  const folderId = folder?.id;
  const userId = useAuthStore((state) => state.user?.id ?? "");
  const { data, isPending, isFetching, isError, refetch } = useQuery({
    queryKey: queryKeys.nodeChildren.folderInfo(folderId ?? "", userId),
    queryFn: () => nodesApi.getFolderStats(folderId ?? "", true),
    enabled: folderId !== undefined && userId.length > 0,
    staleTime: 0,
    refetchOnMount: "always",
  });

  return (
    <Dialog open={folder !== null} onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>{t("folderInfo.title", { name: folder?.name })}</DialogTitle>
      <DialogContent dividers>
        {folder && (
          <Stack spacing={1.5}>
            <Typography>
              {t("folderInfo.created", { date: dateFormatter.format(new Date(folder.createdAt)) })}
            </Typography>
            <Typography>
              {t("folderInfo.modified", { date: dateFormatter.format(new Date(folder.updatedAt)) })}
            </Typography>
            {(isPending || isFetching) && (
              <Box display="flex" alignItems="center" gap={1} py={2}>
                <CircularProgress size={20} />
                <Typography>{t("folderInfo.calculating")}</Typography>
              </Box>
            )}
            {isError && (
              <Stack spacing={1}>
                <Typography color="error">{t("folderInfo.loadFailed")}</Typography>
                <Button onClick={() => void refetch()} variant="outlined">
                  {t("common:actions.retry")}
                </Button>
              </Stack>
            )}
            {data && !isError && !isFetching && (
              <>
                <Typography>{t("folderInfo.folders", { count: data.folders })}</Typography>
                <Typography>{t("folderInfo.files", { count: data.files })}</Typography>
                <Typography>
                  {t("folderInfo.size", { size: formatBytes(data.sizeBytes) })}
                </Typography>
              </>
            )}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t("common:actions.close")}</Button>
      </DialogActions>
    </Dialog>
  );
};
