import React from "react";
import { Box, IconButton } from "@mui/material";
import {
  ContentCut,
  Delete,
  Download,
  Edit,
  History,
  LockOpenOutlined,
  LockOutlined,
  Restore,
  Share,
  Star,
  StarBorder,
} from "@mui/icons-material";
import type { GridColDef } from "@mui/x-data-grid";
import {
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
} from "@shared/crypto";
import type { ColumnOptions, FileListRow } from "./fileListColumnTypes";

type RowActionButton = {
  key: string;
  icon: React.ReactNode;
  title: string;
  onClick: () => void;
};

const actionButton = (action: RowActionButton): React.ReactElement => (
  <IconButton
    key={action.key}
    size="small"
    onClick={(event) => {
      event.stopPropagation();
      action.onClick();
    }}
    title={action.title}
  >
    {action.icon}
  </IconButton>
);

const actionsCell = (
  actions: ReadonlyArray<RowActionButton>,
): React.ReactElement => (
  <Box
    sx={{
      display: "flex",
      alignItems: "center",
      height: "100%",
      width: "100%",
      gap: 0.5,
      justifyContent: "flex-end",
    }}
  >
    {actions.map(actionButton)}
  </Box>
);

type FolderActionOptions = Pick<
  ColumnOptions,
  "labels" | "folderOperations" | "readOnly"
>;

const buildFolderPinAction = (
  row: FileListRow,
  options: FolderActionOptions,
): RowActionButton | null => {
  const operations = options.folderOperations;
  if (!operations.onTogglePin) {
    return null;
  }

  const pinned = operations.isPinned?.(row.id) ?? false;
  return {
    key: "pin",
    icon: pinned ? <Star fontSize="small" /> : <StarBorder fontSize="small" />,
    title: pinned ? options.labels.unpin : options.labels.pin,
    onClick: () => operations.onTogglePin?.(row.id),
  };
};

const buildFolderEncryptionAction = (
  row: FileListRow,
  options: FolderActionOptions,
): RowActionButton | null => {
  const operations = options.folderOperations;
  if (
    !operations.onToggleEncryptionPolicy ||
    row.encryptionPolicy?.inheritedEnabled
  ) {
    return null;
  }

  const enabled =
    row.encryptionPolicy?.explicitEnabled ??
    isFolderEncryptionPolicyEnabled(row.metadata);
  return {
    key: "toggle-encryption",
    icon: enabled ? (
      <LockOpenOutlined fontSize="small" />
    ) : (
      <LockOutlined fontSize="small" />
    ),
    title: enabled
      ? options.labels.disableEncryptionPolicy
      : options.labels.enableEncryptionPolicy,
    onClick: () => operations.onToggleEncryptionPolicy?.(row.id, enabled),
  };
};

const buildFolderActionButtons = (
  row: FileListRow,
  options: FolderActionOptions,
): RowActionButton[] => {
  const operations = options.folderOperations;
  const actions: RowActionButton[] = [];
  if (operations.onDownload) {
    actions.push({
      key: "download",
      icon: <Download fontSize="small" />,
      title: options.labels.download,
      onClick: () => operations.onDownload?.(row.id, row.name),
    });
  }

  if (options.readOnly) {
    return actions;
  }

  const pinAction = buildFolderPinAction(row, options);
  if (pinAction) {
    actions.push(pinAction);
  }

  if (operations.onStartRename) {
    actions.push({
      key: "rename",
      icon: <Edit fontSize="small" />,
      title: options.labels.rename,
      onClick: () => operations.onStartRename?.(row.id, row.name),
    });
  }
  if (operations.onShare) {
    actions.push({
      key: "share",
      icon: <Share fontSize="small" />,
      title: options.labels.share,
      onClick: () => operations.onShare?.(row.id, row.name),
    });
  }
  if (operations.onCut) {
    actions.push({
      key: "cut",
      icon: <ContentCut fontSize="small" />,
      title: options.labels.cut,
      onClick: () => operations.onCut?.(row.id),
    });
  }
  const encryptionAction = buildFolderEncryptionAction(row, options);
  if (encryptionAction) {
    actions.push(encryptionAction);
  }
  if (operations.onRestore) {
    actions.push({
      key: "restore",
      icon: <Restore fontSize="small" />,
      title: options.labels.restore,
      onClick: () => operations.onRestore?.(row.id, row.name),
    });
  }
  if (operations.onDelete) {
    actions.push({
      key: "delete",
      icon: <Delete fontSize="small" />,
      title: options.labels.delete,
      onClick: () => operations.onDelete?.(row.id, row.name),
    });
  }

  return actions;
};

const buildFileActionButtons = (
  row: FileListRow,
  options: Pick<ColumnOptions, "labels" | "fileOperations" | "readOnly">,
): RowActionButton[] => {
  const operations = options.fileOperations;
  const actions: RowActionButton[] = [];
  const fileEncrypted = isFileEncrypted(row.metadata);

  if (operations.onDownload) {
    actions.push({
      key: "download",
      icon: <Download fontSize="small" />,
      title: options.labels.download,
      onClick: () => operations.onDownload?.(row.id, row.name),
    });
  }

  if (operations.onVersions) {
    actions.push({
      key: "versions",
      icon: <History fontSize="small" />,
      title: options.labels.versions,
      onClick: () => operations.onVersions?.(row.id, row.name),
    });
  }

  if (options.readOnly) {
    return actions;
  }

  if (operations.onShare && !fileEncrypted) {
    actions.push({
      key: "share",
      icon: <Share fontSize="small" />,
      title: options.labels.share,
      onClick: () => operations.onShare?.(row.id, row.name),
    });
  }
  if (operations.onStartRename) {
    actions.push({
      key: "rename",
      icon: <Edit fontSize="small" />,
      title: options.labels.rename,
      onClick: () => operations.onStartRename?.(row.id, row.name),
    });
  }
  if (operations.onCut) {
    actions.push({
      key: "cut",
      icon: <ContentCut fontSize="small" />,
      title: options.labels.cut,
      onClick: () => operations.onCut?.(row.id),
    });
  }
  if (operations.onRestore) {
    actions.push({
      key: "restore",
      icon: <Restore fontSize="small" />,
      title: options.labels.restore,
      onClick: () => operations.onRestore?.(row.id, row.name),
    });
  }
  if (operations.onDelete) {
    actions.push({
      key: "delete",
      icon: <Delete fontSize="small" />,
      title: options.labels.delete,
      onClick: () => operations.onDelete?.(row.id, row.name),
    });
  }

  return actions;
};

export const createActionsColumn = (
  options: Pick<
    ColumnOptions,
    "labels" | "folderOperations" | "fileOperations" | "readOnly"
  >,
): GridColDef<FileListRow> => ({
  field: "actions",
  headerName: options.labels.actionsTitle,
  minWidth: 220,
  sortable: false,
  align: "right",
  headerAlign: "right",
  renderCell: (params) => {
    const row = params.row;
    switch (row.type) {
      case "new-folder":
        return null;
      case "folder":
        return actionsCell(buildFolderActionButtons(row, options));
      case "file":
        return actionsCell(buildFileActionButtons(row, options));
    }
  },
});
