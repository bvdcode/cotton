import React, { useMemo } from "react";
import { Box, IconButton, TextField, Typography } from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
import type { GridColDef, GridRowParams, GridRowsProp } from "@mui/x-data-grid";
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
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { formatBytes } from "../../utils/formatBytes";
import {
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "../../utils/fileTypes";
import type { FileSystemTile, IFileListView } from "../../types/FileListViewTypes";

interface FileListRow {
  id: string;
  type: "folder" | "file" | "new-folder";
  name: string;
  sizeBytes: number | null;
  tile?: FileSystemTile;
}

export const ListView: React.FC<IFileListView> = ({
  tiles,
  folderOperations,
  fileOperations,
  isCreatingFolder,
  newFolderName,
  onNewFolderNameChange,
  onConfirmNewFolder,
  onCancelNewFolder,
  folderNamePlaceholder,
  fileNamePlaceholder,
  pagination,
}) => {
  const { t } = useTranslation("files");

  const rows: GridRowsProp<FileListRow> = useMemo(() => {
    const baseRows: FileListRow[] = tiles.map((tile) => {
      if (tile.kind === "folder") {
        return {
          id: tile.node.id,
          type: "folder",
          name: tile.node.name,
          sizeBytes: null,
          tile,
        };
      }

      return {
        id: tile.file.id,
        type: "file",
        name: tile.file.name,
        sizeBytes: tile.file.sizeBytes,
        tile,
      };
    });

    if (!isCreatingFolder) return baseRows;

    return [
      {
        id: "__new_folder__",
        type: "new-folder",
        name: newFolderName,
        sizeBytes: null,
      },
      ...baseRows,
    ];
  }, [tiles, isCreatingFolder, newFolderName]);

  const getFileIcon = (fileName: string) => {
    if (isTextFile(fileName)) return <Article color="action" fontSize="small" />;
    if (isImageFile(fileName)) return <ImageIcon color="action" fontSize="small" />;
    if (isVideoFile(fileName)) return <VideoFile color="action" fontSize="small" />;
    if (isPdfFile(fileName)) return <TextSnippet color="action" fontSize="small" />;
    return <InsertDriveFile color="action" fontSize="small" />;
  };

  const columns: GridColDef<FileListRow>[] = useMemo(
    () => [
      {
        field: "icon",
        headerName: "",
        width: 44,
        sortable: false,
        renderCell: (params) => {
          if (params.row.type === "folder" || params.row.type === "new-folder") {
            return <Folder color="primary" fontSize="small" />;
          }
          return getFileIcon(params.row.name);
        },
      },
      {
        field: "name",
        headerName: t("name"),
        flex: 1,
        minWidth: 220,
        renderCell: (params) => {
          const row = params.row;

          if (row.type === "new-folder") {
            return (
              <TextField
                autoFocus
                fullWidth
                size="small"
                value={newFolderName}
                onChange={(e) => onNewFolderNameChange(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    void onConfirmNewFolder();
                  } else if (e.key === "Escape") {
                    onCancelNewFolder();
                  }
                }}
                onBlur={onConfirmNewFolder}
                placeholder={folderNamePlaceholder}
                variant="standard"
              />
            );
          }

          if (row.type === "folder" && folderOperations.isRenaming(row.id)) {
            return (
              <TextField
                autoFocus
                fullWidth
                size="small"
                value={folderOperations.getRenamingName()}
                onChange={(e) =>
                  folderOperations.onRenamingNameChange(e.target.value)
                }
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    folderOperations.onConfirmRename();
                  } else if (e.key === "Escape") {
                    folderOperations.onCancelRename();
                  }
                }}
                onBlur={folderOperations.onConfirmRename}
                variant="standard"
                onClick={(e) => e.stopPropagation()}
              />
            );
          }

          if (row.type === "file" && fileOperations.isRenaming(row.id)) {
            return (
              <TextField
                autoFocus
                fullWidth
                size="small"
                value={fileOperations.getRenamingName()}
                onChange={(e) =>
                  fileOperations.onRenamingNameChange(e.target.value)
                }
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    void fileOperations.onConfirmRename();
                  } else if (e.key === "Escape") {
                    fileOperations.onCancelRename();
                  }
                }}
                onBlur={() => {
                  void fileOperations.onConfirmRename();
                }}
                placeholder={fileNamePlaceholder}
                variant="standard"
                onClick={(e) => e.stopPropagation()}
              />
            );
          }

          return (
            <Typography
              variant="body2"
              noWrap
              sx={{ overflow: "hidden", textOverflow: "ellipsis" }}
            >
              {row.name}
            </Typography>
          );
        },
      },
      {
        field: "sizeBytes",
        headerName: t("size"),
        width: 140,
        renderCell: (params) => {
          if (params.row.sizeBytes == null) {
            return (
              <Typography variant="body2" color="text.secondary">
                —
              </Typography>
            );
          }
          return (
            <Typography variant="body2" color="text.secondary">
              {formatBytes(params.row.sizeBytes)}
            </Typography>
          );
        },
      },
      {
        field: "actions",
        headerName: t("actionsTitle"),
        width: 140,
        sortable: false,
        align: "right",
        headerAlign: "right",
        renderCell: (params) => {
          const row = params.row;
          if (row.type === "new-folder") return null;

          if (row.type === "folder") {
            return (
              <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    folderOperations.onStartRename(row.id, row.name);
                  }}
                  title="Rename"
                >
                  <Edit fontSize="small" />
                </IconButton>
                <IconButton
                  size="small"
                  onClick={(e) => {
                    e.stopPropagation();
                    folderOperations.onDelete(row.id, row.name);
                  }}
                  title="Delete"
                >
                  <Delete fontSize="small" />
                </IconButton>
              </Box>
            );
          }

          return (
            <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  fileOperations.onDownload(row.id, row.name);
                }}
                title="Download"
              >
                <Download fontSize="small" />
              </IconButton>
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  fileOperations.onStartRename(row.id, row.name);
                }}
                title="Rename"
              >
                <Edit fontSize="small" />
              </IconButton>
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  fileOperations.onDelete(row.id, row.name);
                }}
                title="Delete"
              >
                <Delete fontSize="small" />
              </IconButton>
            </Box>
          );
        },
      },
    ],
    [
      t,
      newFolderName,
      onNewFolderNameChange,
      onConfirmNewFolder,
      onCancelNewFolder,
      folderNamePlaceholder,
      fileNamePlaceholder,
      folderOperations,
      fileOperations,
    ],
  );

  const handleRowClick = (params: GridRowParams<FileListRow>) => {
    const row = params.row;
    if (row.type === "new-folder") return;

    if (row.type === "folder") {
      if (!folderOperations.isRenaming(row.id)) {
        folderOperations.onClick(row.id);
      }
      return;
    }

    if (!fileOperations.isRenaming(row.id)) {
      const isImage = isImageFile(row.name);
      const isVideo = isVideoFile(row.name);
      if (isImage || isVideo) {
        fileOperations.onMediaClick?.(row.id);
      } else {
        fileOperations.onClick(row.id, row.name);
      }
    }
  };

  return (
    <Box sx={{ width: "100%" }}>
      <DataGrid
        autoHeight
        density="compact"
        rows={rows}
        columns={columns}
        disableRowSelectionOnClick
        onRowClick={handleRowClick}
        hideFooter={!pagination}
        paginationMode={pagination ? "server" : "client"}
        pageSizeOptions={pagination ? [10, 25, 50, 100] : []}
        paginationModel={
          pagination
            ? { page: pagination.page, pageSize: pagination.pageSize }
            : undefined
        }
        onPaginationModelChange={
          pagination
            ? (model) => {
                if (model.page !== pagination.page) {
                  pagination.onPageChange(model.page);
                }
                if (model.pageSize !== pagination.pageSize) {
                  pagination.onPageSizeChange(model.pageSize);
                }
              }
            : undefined
        }
        rowCount={pagination ? pagination.totalCount : rows.length}
        loading={pagination?.loading}
      />
    </Box>
  );
};
