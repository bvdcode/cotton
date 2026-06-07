import React from "react";
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { Delete, Download, Restore } from "@mui/icons-material";
import { useConfirm } from "material-ui-confirm";
import { destructiveConfirmOptions } from "@shared/ui/confirmOptions";
import { useTranslation } from "react-i18next";
import { filesApi, type FileVersionDto } from "../../../shared/api/filesApi";
import {
  useDeleteFileVersionMutation,
  useFileVersionsQuery,
  useRestoreFileVersionMutation,
} from "../../../shared/api/queries/fileVersions";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { openDownloadLink } from "../../../shared/utils/fileHandlers";

interface FileVersionsDialogProps {
  open: boolean;
  fileId: string | null;
  fileName: string;
  onClose: () => void;
  onRestored: () => void;
}

type BusyAction = "restore" | "delete" | "download";

interface BusyState {
  versionId: string;
  action: BusyAction;
}

export const FileVersionsDialog: React.FC<FileVersionsDialogProps> = ({
  open,
  fileId,
  fileName,
  onClose,
  onRestored,
}) => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const {
    data: versions = [],
    isError,
    isFetching,
    isLoading,
    refetch,
  } = useFileVersionsQuery(fileId, open);
  const { mutateAsync: restoreFileVersion } = useRestoreFileVersionMutation();
  const { mutateAsync: deleteFileVersion } = useDeleteFileVersionMutation();
  const [actionError, setActionError] = React.useState<{
    fileId: string;
    message: string;
  } | null>(null);
  const [busy, setBusy] = React.useState<BusyState | null>(null);

  const loading = isLoading || isFetching;
  const error = actionError?.fileId === fileId
    ? actionError.message
    : isError
      ? t("fileVersions.loadFailed", { ns: "files" })
      : null;

  const retryLoad = React.useCallback(() => {
    setActionError(null);
    void refetch();
  }, [refetch]);

  const restoreVersion = React.useCallback(
    async (version: FileVersionDto) => {
      if (!fileId || version.isCurrent) return;

      const result = await confirm({
        title: t("fileVersions.restoreConfirmTitle", { ns: "files" }),
        description: t("fileVersions.restoreConfirmDescription", {
          ns: "files",
          version: version.versionNumber,
        }),
        confirmationText: t("common:actions.restore"),
        cancellationText: t("common:actions.cancel"),
      });
      if (!result.confirmed) return;

      setBusy({ versionId: version.id, action: "restore" });
      try {
        setActionError(null);
        await restoreFileVersion({ fileId, versionId: version.id });
        onRestored();
        await refetch();
      } catch (restoreError) {
        console.error("Failed to restore file version:", restoreError);
        setActionError({
          fileId,
          message: t("fileVersions.restoreFailed", { ns: "files" }),
        });
      } finally {
        setBusy(null);
      }
    },
    [confirm, fileId, onRestored, refetch, restoreFileVersion, t],
  );

  const deleteVersion = React.useCallback(
    async (version: FileVersionDto) => {
      if (!fileId || !version.canDelete) return;

      const result = await confirm({
        title: t("fileVersions.deleteConfirmTitle", { ns: "files" }),
        description: t("fileVersions.deleteConfirmDescription", {
          ns: "files",
          version: version.versionNumber,
        }),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        ...destructiveConfirmOptions,
      });
      if (!result.confirmed) return;

      setBusy({ versionId: version.id, action: "delete" });
      try {
        setActionError(null);
        await deleteFileVersion({ fileId, versionId: version.id });
        await refetch();
      } catch (deleteError) {
        console.error("Failed to delete file version:", deleteError);
        setActionError({
          fileId,
          message: t("fileVersions.deleteFailed", { ns: "files" }),
        });
      } finally {
        setBusy(null);
      }
    },
    [confirm, deleteFileVersion, fileId, refetch, t],
  );

  const downloadVersion = React.useCallback(
    async (version: FileVersionDto) => {
      if (!fileId) return;

      setBusy({ versionId: version.id, action: "download" });
      try {
        const link = version.isCurrent
          ? await filesApi.getDownloadLink(fileId)
          : await filesApi.getVersionDownloadLink(fileId, version.id);
        openDownloadLink(link, version.name);
      } catch (downloadError) {
        console.error("Failed to download file version:", downloadError);
        setActionError({
          fileId,
          message: t("fileVersions.downloadFailed", { ns: "files" }),
        });
      } finally {
        setBusy(null);
      }
    },
    [fileId, t],
  );

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{t("fileVersions.title", { ns: "files", name: fileName })}</DialogTitle>
      <DialogContent dividers>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress />
          </Box>
        ) : null}

        {!loading && error ? (
          <Stack spacing={2} sx={{ py: 2 }}>
            <Typography color="error">{error}</Typography>
            <Button onClick={retryLoad} variant="outlined">
              {t("common:actions.reload")}
            </Button>
          </Stack>
        ) : null}

        {!loading && !error && versions.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 2 }}>
            {t("fileVersions.empty", { ns: "files" })}
          </Typography>
        ) : null}

        {!loading && !error && versions.length > 0 ? (
          <List disablePadding>
            {versions.map((version) => (
              <VersionListItem
                key={version.id}
                busy={busy}
                onDelete={() => void deleteVersion(version)}
                onDownload={() => void downloadVersion(version)}
                onRestore={() => void restoreVersion(version)}
                t={t}
                version={version}
              />
            ))}
          </List>
        ) : null}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t("common:actions.close")}</Button>
      </DialogActions>
    </Dialog>
  );
};

interface VersionListItemProps {
  version: FileVersionDto;
  busy: BusyState | null;
  t: ReturnType<typeof useTranslation<["files", "common"]>>["t"];
  onRestore: () => void;
  onDelete: () => void;
  onDownload: () => void;
}

const VersionListItem: React.FC<VersionListItemProps> = ({
  version,
  busy,
  t,
  onRestore,
  onDelete,
  onDownload,
}) => {
  const busyAction = busy?.versionId === version.id ? busy.action : null;
  const disabled = busy !== null;
  const createdAt = new Date(version.createdAt).toLocaleString();

  return (
    <ListItem
      divider
      secondaryAction={(
        <Stack direction="row" spacing={0.5}>
          <Tooltip title={t("common:actions.download")}>
            <span>
              <IconButton
                edge="end"
                onClick={onDownload}
                disabled={disabled}
                aria-label={t("common:actions.download")}
              >
                {busyAction === "download" ? <CircularProgress size={20} /> : <Download />}
              </IconButton>
            </span>
          </Tooltip>
          {!version.isCurrent ? (
            <Tooltip title={t("common:actions.restore")}>
              <span>
                <IconButton
                  edge="end"
                  onClick={onRestore}
                  disabled={disabled}
                  aria-label={t("common:actions.restore")}
                >
                  {busyAction === "restore" ? <CircularProgress size={20} /> : <Restore />}
                </IconButton>
              </span>
            </Tooltip>
          ) : null}
          {version.canDelete ? (
            <Tooltip title={t("common:actions.delete")}>
              <span>
                <IconButton
                  color="error"
                  edge="end"
                  onClick={onDelete}
                  disabled={disabled}
                  aria-label={t("common:actions.delete")}
                >
                  {busyAction === "delete" ? <CircularProgress size={20} /> : <Delete />}
                </IconButton>
              </span>
            </Tooltip>
          ) : null}
        </Stack>
      )}
    >
      <ListItemText
        primary={(
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", pr: 12 }}>
            <Typography component="span" fontWeight={600} noWrap>
              {t("fileVersions.versionLabel", {
                ns: "files",
                version: version.versionNumber,
              })}
            </Typography>
            {version.isCurrent ? <Chip size="small" label={t("fileVersions.current", { ns: "files" })} /> : null}
            {version.isOriginal ? <Chip size="small" label={t("fileVersions.original", { ns: "files" })} /> : null}
          </Stack>
        )}
        primaryTypographyProps={{ component: "div" }}
        secondary={(
          <Stack spacing={0.25} sx={{ mt: 0.75, pr: 12 }}>
            <Typography color="text.secondary" component="span" variant="body2">
              {createdAt}
            </Typography>
            <Typography color="text.secondary" component="span" variant="body2">
              {formatBytes(version.sizeBytes)} / {version.contentType}
            </Typography>
          </Stack>
        )}
        secondaryTypographyProps={{ component: "div" }}
      />
    </ListItem>
  );
};
