import React from "react";
import {
  Box,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  IconButton,
  Typography,
} from "@mui/material";
import {
  Folder,
  InsertDriveFile,
  Download,
  Edit,
  Delete,
  Image as ImageIcon,
  VideoFile,
  Article,
  TextSnippet,
} from "@mui/icons-material";
import { formatBytes } from "../../utils/formatBytes";
import {
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "../../utils/fileTypes";
import type { IFileListView } from "../../types/FileListViewTypes";
import { useTranslation } from "react-i18next";

/**
 * ListView Component
 *
 * Displays files and folders in a table/list layout.
 * Follows the Dependency Inversion Principle (DIP) by depending on the IFileListView interface.
 * Single Responsibility Principle (SRP): Responsible only for rendering the table layout.
 */
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
}) => {
  const { t } = useTranslation("files");

  return (
    <TableContainer component={Box}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell width="40px"></TableCell>
            <TableCell>{t("name")}</TableCell>
            <TableCell
              width="120px"
              sx={{ display: { xs: "none", sm: "table-cell" } }}
            >
              {t("size")}
            </TableCell>
            <TableCell width="120px" align="right">
              {t("actionsTitle")}
            </TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {/* New Folder Creation Row */}
          {isCreatingFolder && (
            <TableRow
              sx={{
                bgcolor: "action.hover",
                "& td": { borderColor: "primary.main" },
              }}
            >
              <TableCell>
                <Folder sx={{ color: "primary.main" }} />
              </TableCell>
              <TableCell colSpan={3}>
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
              </TableCell>
            </TableRow>
          )}

          {/* Render all tiles (folders and files) */}
          {tiles.map((tile) => {
            if (tile.kind === "folder") {
              const isRenaming = folderOperations.isRenaming(tile.node.id);

              return (
                <TableRow
                  key={tile.node.id}
                  hover={!isRenaming}
                  sx={{
                    cursor: isRenaming ? "default" : "pointer",
                    bgcolor: isRenaming ? "action.hover" : undefined,
                  }}
                  onClick={
                    isRenaming
                      ? undefined
                      : () => folderOperations.onClick(tile.node.id)
                  }
                >
                  <TableCell>
                    <Folder color="primary" />
                  </TableCell>
                  <TableCell>
                    {isRenaming ? (
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
                    ) : (
                      <Typography variant="body2">{tile.node.name}</Typography>
                    )}
                  </TableCell>
                  <TableCell sx={{ display: { xs: "none", sm: "table-cell" } }}>
                    <Typography variant="body2" color="text.secondary">
                      —
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Box
                      sx={{
                        display: "flex",
                        gap: 0.5,
                        justifyContent: "flex-end",
                      }}
                    >
                      <IconButton
                        size="small"
                        onClick={(e) => {
                          e.stopPropagation();
                          folderOperations.onStartRename(
                            tile.node.id,
                            tile.node.name,
                          );
                        }}
                        title="Rename"
                      >
                        <Edit fontSize="small" />
                      </IconButton>
                      <IconButton
                        size="small"
                        onClick={(e) => {
                          e.stopPropagation();
                          folderOperations.onDelete(
                            tile.node.id,
                            tile.node.name,
                          );
                        }}
                        title="Delete"
                      >
                        <Delete fontSize="small" />
                      </IconButton>
                    </Box>
                  </TableCell>
                </TableRow>
              );
            }

            // File row rendering
            const isImage = isImageFile(tile.file.name);
            const isVideo = isVideoFile(tile.file.name);
            const isText = isTextFile(tile.file.name);
            const isPdf = isPdfFile(tile.file.name);
            const isRenaming = fileOperations.isRenaming(tile.file.id);

            const getFileIcon = () => {
              if (isText) {
                return <Article color="action" />;
              }
              if (isImage) {
                return <ImageIcon color="action" />;
              }
              if (isVideo) {
                return <VideoFile color="action" />;
              }
              if (isPdf) {
                return <TextSnippet color="action" />;
              }
              return <InsertDriveFile color="action" />;
            };

            return (
              <TableRow
                key={tile.file.id}
                hover={!isRenaming}
                sx={{
                  cursor: isRenaming ? "default" : "pointer",
                  bgcolor: isRenaming ? "action.hover" : undefined,
                }}
                onClick={
                  isRenaming
                    ? undefined
                    : isImage || isVideo
                      ? () => fileOperations.onMediaClick?.(tile.file.id)
                      : () =>
                          fileOperations.onClick(tile.file.id, tile.file.name)
                }
              >
                <TableCell>{getFileIcon()}</TableCell>
                <TableCell>
                  {isRenaming ? (
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
                  ) : (
                    <Typography variant="body2">{tile.file.name}</Typography>
                  )}
                </TableCell>
                <TableCell sx={{ display: { xs: "none", sm: "table-cell" } }}>
                  <Typography variant="body2" color="text.secondary">
                    {formatBytes(tile.file.sizeBytes)}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Box
                    sx={{
                      display: "flex",
                      gap: 0.5,
                      justifyContent: "flex-end",
                    }}
                  >
                    <IconButton
                      size="small"
                      onClick={(e) => {
                        e.stopPropagation();
                        fileOperations.onDownload(tile.file.id, tile.file.name);
                      }}
                      title="Download"
                    >
                      <Download fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      onClick={(e) => {
                        e.stopPropagation();
                        fileOperations.onStartRename(
                          tile.file.id,
                          tile.file.name,
                        );
                      }}
                      title="Rename"
                    >
                      <Edit fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      onClick={(e) => {
                        e.stopPropagation();
                        fileOperations.onDelete(tile.file.id, tile.file.name);
                      }}
                      title="Delete"
                    >
                      <Delete fontSize="small" />
                    </IconButton>
                  </Box>
                </TableCell>
              </TableRow>
            );
          })}

          {/* Empty state */}
          {tiles.length === 0 && !isCreatingFolder && (
            <TableRow>
              <TableCell colSpan={4} align="center">
                <Typography color="text.secondary" sx={{ py: 3 }}>
                  No files or folders
                </Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
};
