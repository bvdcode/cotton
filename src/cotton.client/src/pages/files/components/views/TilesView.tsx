import React from "react";
import { Box, TextField, Typography } from "@mui/material";
import { Folder } from "@mui/icons-material";
import { FolderCard } from "../FolderCard";
import { RenamableItemCard } from "../RenamableItemCard";
import { getFileIcon } from "../../utils/icons";
import { formatBytes } from "../../utils/formatBytes";
import {
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "../../utils/fileTypes";
import { Download, Edit, Delete, Share } from "@mui/icons-material";
import type { IFileListView } from "../../types/FileListViewTypes";
import { alpha, useTheme } from "@mui/material/styles";
import Loader from "../../../../shared/ui/Loader";
import { useTranslation } from "react-i18next";

/**
 * TilesView Component
 *
 * Displays files and folders in a responsive grid layout (tiles/cards).
 * Follows the Dependency Inversion Principle (DIP) by depending on the IFileListView interface.
 * Single Responsibility Principle (SRP): Responsible only for rendering the grid layout.
 */
export const TilesView: React.FC<IFileListView> = ({
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
  emptyStateText,
  loading = false,
  loadingTitle,
  loadingCaption,
  tileSize = "medium",
}) => {
  const theme = useTheme();
  const isDarkMode = theme.palette.mode === "dark";
  const { t } = useTranslation(["common"]);

  const gridTemplateColumns =
    tileSize === "small"
      ? {
          xs: "repeat(4, minmax(0, 1fr))",
          sm: "repeat(5, minmax(0, 1fr))",
          md: "repeat(6, minmax(0, 1fr))",
          lg: "repeat(8, minmax(0, 1fr))",
          xl: "repeat(10, minmax(0, 1fr))",
        }
      : tileSize === "large"
        ? {
            xs: "repeat(3, minmax(0, 1fr))",
            sm: "repeat(3, minmax(0, 1fr))",
            md: "repeat(4, minmax(0, 1fr))",
            lg: "repeat(5, minmax(0, 1fr))",
            xl: "repeat(6, minmax(0, 1fr))",
          }
        : {
            xs: "repeat(3, minmax(0, 1fr))",
            sm: "repeat(4, minmax(0, 1fr))",
            md: "repeat(5, minmax(0, 1fr))",
            lg: "repeat(6, minmax(0, 1fr))",
            xl: "repeat(8, minmax(0, 1fr))",
          };

  const gridGap =
    tileSize === "small"
      ? { xs: 0.75, sm: 1 }
      : tileSize === "large"
        ? { xs: 1.25, sm: 2 }
        : { xs: 1, sm: 1.5 };

  if (!loading && !isCreatingFolder && tiles.length === 0 && emptyStateText) {
    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          minHeight: 160,
        }}
      >
        <Typography color="text.secondary">{emptyStateText}</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ position: "relative", pb: { xs: 1, sm: 3 } }}>
      {loading && tiles.length === 0 && (
        <Box
          sx={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            minHeight: 200,
            bgcolor: "background.default",
            zIndex: 10,
          }}
        >
          <Loader title={loadingTitle} caption={loadingCaption} />
        </Box>
      )}
      <Box
        sx={{
          display: "grid",
          gap: gridGap,
          gridTemplateColumns,
        }}
      >
        {/* New Folder Creation Card */}
        {isCreatingFolder && (
          <Box
            sx={{
              border: "2px solid",
              borderColor: "primary.main",
              borderRadius: 1,
              p: { xs: 1, sm: 1.25, md: 1 },
              bgcolor: "action.hover",
            }}
          >
            <Box
              sx={{
                width: "100%",
                aspectRatio: "1 / 1",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                borderRadius: 1.5,
                overflow: "hidden",
                "& > svg": {
                  width: "70%",
                  height: "70%",
                },
              }}
            >
              <Folder sx={{ color: "primary.main" }} />
            </Box>
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
              slotProps={{
                input: {
                  sx: { fontSize: { xs: "0.8rem", md: "0.85rem" } },
                },
              }}
            />
          </Box>
        )}

        {/* Render all tiles (folders and files) */}
        {tiles.map((tile) => {
          if (tile.kind === "folder") {
            return (
              <FolderCard
                key={tile.node.id}
                folder={tile.node}
                isRenaming={folderOperations.isRenaming(tile.node.id)}
                renamingName={folderOperations.getRenamingName()}
                onRenamingNameChange={folderOperations.onRenamingNameChange}
                onConfirmRename={folderOperations.onConfirmRename}
                onCancelRename={folderOperations.onCancelRename}
                onStartRename={() =>
                  folderOperations.onStartRename(tile.node.id, tile.node.name)
                }
                onDelete={() =>
                  folderOperations.onDelete(tile.node.id, tile.node.name)
                }
                onClick={() => folderOperations.onClick(tile.node.id)}
              />
            );
          }

          // File tile rendering
          const isImage = isImageFile(tile.file.name);
          const isVideo = isVideoFile(tile.file.name);
          const shouldLightenPreviewBackdrop =
            isDarkMode &&
            (isPdfFile(tile.file.name) || isTextFile(tile.file.name));
          const preview = getFileIcon(
            tile.file.encryptedFilePreviewHashHex ?? null,
            tile.file.name,
            tile.file.contentType,
          );
          const previewUrl = typeof preview === "string" ? preview : null;

          const iconContainerSx = previewUrl
            ? {
                ...(shouldLightenPreviewBackdrop && {
                  bgcolor: alpha(theme.palette.common.white, 0.75),
                }),
              }
            : undefined;

          return (
            <RenamableItemCard
              key={tile.file.id}
              icon={(() => {
                if (previewUrl && (isImage || isVideo)) {
                  return (
                    <Box
                      sx={{
                        width: "100%",
                        height: "100%",
                        position: "relative",
                      }}
                    >
                      <Box
                        component="img"
                        src={previewUrl}
                        alt=""
                        aria-hidden
                        sx={{
                          position: "absolute",
                          inset: 0,
                          display: "block",
                          width: "100%",
                          height: "100%",
                          objectFit: "cover",
                          filter: "blur(24px)",
                          transform: "scale(1.15)",
                          opacity: 0.6,
                        }}
                      />
                      <Box
                        component="img"
                        src={previewUrl}
                        alt={tile.file.name}
                        loading="lazy"
                        decoding="async"
                        sx={{
                          position: "relative",
                          display: "block",
                          width: "100%",
                          height: "100%",
                          objectFit: "contain",
                          cursor: isImage || isVideo ? "pointer" : "default",
                        }}
                      />
                    </Box>
                  );
                }
                if (previewUrl) {
                  const isTextPreview = isTextFile(tile.file.name);
                  return (
                    <Box
                      sx={{
                        width: "100%",
                        height: "100%",
                        position: "relative",
                      }}
                    >
                      <Box
                        component="img"
                        src={previewUrl}
                        alt=""
                        aria-hidden
                        sx={{
                          position: "absolute",
                          inset: 0,
                          display: "block",
                          width: "100%",
                          height: "100%",
                          objectFit: "cover",
                          filter: "blur(24px)",
                          transform: "scale(1.15)",
                          opacity: 0.5,
                        }}
                      />
                      <Box
                        component="img"
                        src={previewUrl}
                        alt={tile.file.name}
                        loading="lazy"
                        decoding="async"
                        sx={(theme) => ({
                          position: "relative",
                          display: "block",
                          width: "100%",
                          height: "100%",
                          objectFit: "contain",
                          ...(shouldLightenPreviewBackdrop && {
                            backgroundColor: alpha(
                              theme.palette.common.white,
                              0.75,
                            ),
                          }),
                          ...(isTextPreview &&
                            theme.palette.mode === "dark" && {
                              filter: "invert(1)",
                            }),
                        })}
                      />
                    </Box>
                  );
                }
                return preview;
              })()}
              title={tile.file.name}
              subtitle={formatBytes(tile.file.sizeBytes)}
              onClick={
                isImage || isVideo
                  ? () => {
                      fileOperations.onMediaClick?.(tile.file.id);
                    }
                  : () =>
                      fileOperations.onClick(
                        tile.file.id,
                        tile.file.name,
                        tile.file.sizeBytes,
                      )
              }
              iconContainerSx={iconContainerSx}
              actions={[
                {
                  icon: <Download />,
                  onClick: () =>
                    fileOperations.onDownload(tile.file.id, tile.file.name),
                  tooltip: t("actions.download", { ns: "common" }),
                },
                {
                  icon: <Share />,
                  onClick: () =>
                    fileOperations.onShare(tile.file.id, tile.file.name),
                  tooltip: t("actions.share", { ns: "common" }),
                },
                {
                  icon: <Edit />,
                  onClick: () =>
                    fileOperations.onStartRename(tile.file.id, tile.file.name),
                  tooltip: t("actions.rename", { ns: "common" }),
                },
                {
                  icon: <Delete />,
                  onClick: () =>
                    fileOperations.onDelete(tile.file.id, tile.file.name),
                  tooltip: t("actions.delete", { ns: "common" }),
                },
              ]}
              isRenaming={fileOperations.isRenaming(tile.file.id)}
              renamingValue={fileOperations.getRenamingName()}
              onRenamingValueChange={fileOperations.onRenamingNameChange}
              onConfirmRename={() => {
                void fileOperations.onConfirmRename();
              }}
              onCancelRename={fileOperations.onCancelRename}
              placeholder={fileNamePlaceholder}
            />
          );
        })}
      </Box>
    </Box>
  );
};
