import React from "react";
import { Box, ButtonBase, IconButton, Typography } from "@mui/material";
import {
  Article,
  Delete,
  Download,
  Edit,
  Folder,
  Image as ImageIcon,
  InsertDriveFile,
  TextSnippet,
  VideoFile,
  Share,
} from "@mui/icons-material";
import type { GridColDef } from "@mui/x-data-grid";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import {
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "../../utils/fileTypes";
import { InlineRenameField } from "../InlineRenameField";

export interface FileListRow {
  id: string;
  type: "folder" | "file" | "new-folder";
  name: string;
  location?: string | null;
  containerPath?: string | null;
  containerNodeId?: string | null;
  sizeBytes: number | null;
  contentType?: string | null;
  tile?: {
    kind: "folder" | "file";
    file?: {
      id: string;
      name: string;
      previewHashEncryptedHex?: string | null;
    };
  };
}

interface ColumnOptions {
  readOnly?: boolean;
  labels: {
    name: string;
    size: string;
    location: string;
    actionsTitle: string;
    placeholder: string;
    goToFolder: string;
    rename: string;
    delete: string;
    download: string;
    share: string;
  };
  newFolderName: string;
  onNewFolderNameChange: (value: string) => void;
  onConfirmNewFolder: () => void;
  onCancelNewFolder: () => void;
  folderNamePlaceholder: string;
  fileNamePlaceholder: string;
  onGoToFileLocation?: (target: { nodeId?: string; containerPath?: string }) => void;
  columnFlex?: {
    name: number;
    location: number;
  };
  folderOperations: {
    isRenaming: (id: string) => boolean;
    getRenamingName: () => string;
    onRenamingNameChange: (value: string) => void;
    onConfirmRename?: () => void;
    onCancelRename?: () => void;
    onStartRename?: (id: string, name: string) => void;
    onDelete?: (id: string, name: string) => void;
    onShare?: (id: string, name: string) => void;
  };
  fileOperations: {
    isRenaming: (id: string) => boolean;
    getRenamingName: () => string;
    onRenamingNameChange: (value: string) => void;
    onConfirmRename?: () => Promise<void>;
    onCancelRename?: () => void;
    onStartRename?: (id: string, name: string) => void;
    onDownload?: (id: string, name: string) => void;
    onShare?: (id: string, name: string) => void;
    onDelete?: (id: string, name: string) => void;
  };
  failedPreviews: Set<string>;
  setFailedPreviews: React.Dispatch<React.SetStateAction<Set<string>>>;
}

const getSmallFileIcon = (fileName: string) => {
  const iconSx = { fontSize: 32 };
  if (isTextFile(fileName)) return <Article color="action" sx={iconSx} />;
  if (isImageFile(fileName)) return <ImageIcon color="action" sx={iconSx} />;
  if (isVideoFile(fileName)) return <VideoFile color="action" sx={iconSx} />;
  if (isPdfFile(fileName)) return <TextSnippet color="action" sx={iconSx} />;
  return <InsertDriveFile color="action" sx={iconSx} />;
};

export const createIconColumn = (
  options: Pick<ColumnOptions, "failedPreviews" | "setFailedPreviews">,
): GridColDef<FileListRow> => ({
  field: "icon",
  headerName: "",
  width: 44,
  sortable: false,
  renderCell: (params) => {
    const previewUrl =
      params.row.type === "file" && params.row.tile?.kind === "file"
        ? params.row.tile.file?.previewHashEncryptedHex
          ? `/api/v1/preview/${encodeURIComponent(
              params.row.tile.file.previewHashEncryptedHex,
            )}.webp`
          : null
        : null;

    const showPreview =
      previewUrl && !options.failedPreviews.has(params.row.id);

    return (
      <Box sx={{ display: "flex", alignItems: "center", height: "100%" }}>
        {params.row.type === "folder" || params.row.type === "new-folder" ? (
          <Folder color="primary" sx={{ fontSize: 32 }} />
        ) : showPreview ? (
          <Box
            component="img"
            src={previewUrl}
            alt=""
            loading="lazy"
            onError={() => {
              options.setFailedPreviews((prev) =>
                new Set(prev).add(params.row.id),
              );
            }}
            sx={{
              width: 32,
              height: 32,
              objectFit: "contain",
              borderRadius: 0.5,
            }}
          />
        ) : (
          getSmallFileIcon(params.row.name)
        )}
      </Box>
    );
  },
});

export const createNameColumn = (
  options: Pick<
    ColumnOptions,
    | "labels"
    | "columnFlex"
    | "newFolderName"
    | "onNewFolderNameChange"
    | "onConfirmNewFolder"
    | "onCancelNewFolder"
    | "folderNamePlaceholder"
    | "fileNamePlaceholder"
    | "folderOperations"
    | "fileOperations"
  >,
): GridColDef<FileListRow> => ({
  field: "name",
  headerName: options.labels.name,
  flex: options.columnFlex?.name ?? 1,
  minWidth: 120,
  renderCell: (params) => {
    const row = params.row;

    if (row.type === "new-folder") {
      return (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            height: "100%",
            width: "100%",
          }}
        >
          <InlineRenameField
            value={options.newFolderName}
            onChange={options.onNewFolderNameChange}
            onConfirm={options.onConfirmNewFolder}
            onCancel={options.onCancelNewFolder}
            placeholder={options.folderNamePlaceholder}
          />
        </Box>
      );
    }

    if (row.type === "folder" && options.folderOperations.isRenaming(row.id)) {
      return (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            height: "100%",
            width: "100%",
          }}
        >
          <InlineRenameField
            value={options.folderOperations.getRenamingName()}
            onChange={options.folderOperations.onRenamingNameChange}
            onConfirm={() => options.folderOperations.onConfirmRename?.()}
            onCancel={() => options.folderOperations.onCancelRename?.()}
          />
        </Box>
      );
    }

    if (row.type === "file" && options.fileOperations.isRenaming(row.id)) {
      return (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            height: "100%",
            width: "100%",
          }}
        >
          <InlineRenameField
            value={options.fileOperations.getRenamingName()}
            onChange={options.fileOperations.onRenamingNameChange}
            onConfirm={() => options.fileOperations.onConfirmRename?.()}
            onCancel={() => options.fileOperations.onCancelRename?.()}
            placeholder={options.fileNamePlaceholder}
          />
        </Box>
      );
    }

    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          height: "100%",
          width: "100%",
        }}
      >
        <Typography
          variant="body2"
          noWrap
          sx={{ overflow: "hidden", textOverflow: "ellipsis" }}
        >
          {row.name}
        </Typography>
      </Box>
    );
  },
});

export const createSizeColumn = (
  options: Pick<ColumnOptions, "labels">,
): GridColDef<FileListRow> => ({
  field: "sizeBytes",
  headerName: options.labels.size,
  width: 70,
  renderCell: (params) => {
    if (params.row.sizeBytes == null) {
      return (
        <Box sx={{ display: "flex", alignItems: "center", height: "100%" }}>
          <Typography variant="body2" color="text.secondary">
            {options.labels.placeholder}
          </Typography>
        </Box>
      );
    }
    return (
      <Box sx={{ display: "flex", alignItems: "center", height: "100%" }}>
        <Typography variant="body2" color="text.secondary">
          {formatBytes(params.row.sizeBytes)}
        </Typography>
      </Box>
    );
  },
});

export const createLocationColumn = (
  options: Pick<ColumnOptions, "labels" | "columnFlex" | "onGoToFileLocation">,
): GridColDef<FileListRow> => ({
  field: "location",
  headerName: options.labels.location,
  flex: options.columnFlex?.location ?? 1,
  minWidth: 120,
  renderCell: (params) => {
    const row = params.row;
    const value = row.location;
    if (!value) {
      return (
        <Box sx={{ display: "flex", alignItems: "center", height: "100%" }}>
          <Typography variant="body2" color="text.secondary">
            {options.labels.placeholder}
          </Typography>
        </Box>
      );
    }

    const canNavigateToLocation =
      row.type === "file" &&
      !!options.onGoToFileLocation &&
      !!(row.containerNodeId || row.containerPath);

    const navigateToLocation = (e: React.MouseEvent) => {
      e.stopPropagation();
      options.onGoToFileLocation?.({
        nodeId: row.containerNodeId ?? undefined,
        containerPath: row.containerPath ?? undefined,
      });
    };

    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          height: "100%",
          width: "100%",
        }}
      >
        {canNavigateToLocation ? (
          <ButtonBase
            onClick={navigateToLocation}
            sx={{
              justifyContent: "flex-start",
              width: "100%",
              textAlign: "left",
              borderRadius: 1,
            }}
          >
            <Typography
              variant="body2"
              color="primary"
              noWrap
              sx={{ overflow: "hidden", textOverflow: "ellipsis" }}
              title={value}
            >
              {value}
            </Typography>
          </ButtonBase>
        ) : (
          <Typography
            variant="body2"
            color="text.secondary"
            noWrap
            sx={{ overflow: "hidden", textOverflow: "ellipsis" }}
            title={value}
          >
            {value}
          </Typography>
        )}
      </Box>
    );
  },
});

export const createActionsColumn = (
  options: Pick<
    ColumnOptions,
    "labels" | "folderOperations" | "fileOperations" | "readOnly"
  >,
): GridColDef<FileListRow> => ({
  field: "actions",
  headerName: options.labels.actionsTitle,
  minWidth: 180,
  sortable: false,
  align: "right",
  headerAlign: "right",
  renderCell: (params) => {
    const row = params.row;
    if (row.type === "new-folder") return null;

    if (row.type === "folder") {
      return (
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
          {!options.readOnly && (
            <>
              {options.folderOperations.onStartRename && (
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    options.folderOperations.onStartRename?.(row.id, row.name);
                  }}
                  title={options.labels.rename}
                >
                  <Edit fontSize="small" />
                </IconButton>
              )}
              {options.folderOperations.onShare && (
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    options.folderOperations.onShare?.(row.id, row.name);
                  }}
                  title={options.labels.share}
                >
                  <Share fontSize="small" />
                </IconButton>
              )}
              {options.folderOperations.onDelete && (
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    options.folderOperations.onDelete?.(row.id, row.name);
                  }}
                  title={options.labels.delete}
                >
                  <Delete fontSize="small" />
                </IconButton>
              )}
            </>
          )}
        </Box>
      );
    }

    return (
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
        {options.fileOperations.onDownload && (
          <IconButton
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              options.fileOperations.onDownload?.(row.id, row.name);
            }}
            title={options.labels.download}
          >
            <Download fontSize="small" />
          </IconButton>
        )}
        {!options.readOnly && (
          <>
            {options.fileOperations.onShare && (
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  options.fileOperations.onShare?.(row.id, row.name);
                }}
                title={options.labels.share}
              >
                <Share fontSize="small" />
              </IconButton>
            )}
            {options.fileOperations.onStartRename && (
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  options.fileOperations.onStartRename?.(row.id, row.name);
                }}
                title={options.labels.rename}
              >
                <Edit fontSize="small" />
              </IconButton>
            )}
            {options.fileOperations.onDelete && (
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  options.fileOperations.onDelete?.(row.id, row.name);
                }}
                title={options.labels.delete}
              >
                <Delete fontSize="small" />
              </IconButton>
            )}
          </>
        )}
      </Box>
    );
  },
});

export const createFileListColumns = (
  options: ColumnOptions,
): GridColDef<FileListRow>[] => {
  const columns: GridColDef<FileListRow>[] = [
    createIconColumn(options),
    createNameColumn(options),
  ];

  if (options.onGoToFileLocation) {
    columns.push(createLocationColumn(options));
  }

  columns.push(createSizeColumn(options), createActionsColumn(options));
  return columns;
};
