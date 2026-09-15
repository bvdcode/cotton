import React from "react";
import { Box, ButtonBase, Typography } from "@mui/material";
import {
  Article,
  Folder,
  Image as ImageIcon,
  InsertDriveFile,
  LockOutlined,
  TextSnippet,
  VideoFile,
} from "@mui/icons-material";
import type { GridColDef } from "@mui/x-data-grid";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import {
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "@shared/utils/fileTypes";
import { InlineRenameField } from "../InlineRenameField";
import {
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
} from "../../../../shared/crypto";
import { buildPreviewUrl } from "@shared/api/previewUrl";
import { createActionsColumn } from "./fileListActionColumn";
import type { ColumnOptions, FileListRow } from "./fileListColumnTypes";

export { createActionsColumn } from "./fileListActionColumn";
export type { FileListRow } from "./fileListColumnTypes";

const getSmallFileIcon = (fileName: string) => {
  const iconSx = { fontSize: 32 };
  if (isTextFile(fileName)) {
    return <Article color="action" sx={iconSx} />;
  }
  if (isImageFile(fileName)) {
    return <ImageIcon color="action" sx={iconSx} />;
  }
  if (isVideoFile(fileName)) {
    return <VideoFile color="action" sx={iconSx} />;
  }
  if (isPdfFile(fileName)) {
    return <TextSnippet color="action" sx={iconSx} />;
  }
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
          ? buildPreviewUrl(params.row.tile.file.previewHashEncryptedHex)
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
            draggable={false}
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

    const encryptionTitle =
      row.type === "file" && isFileEncrypted(row.metadata)
        ? options.labels.encryptedFile
        : row.type === "folder" &&
            (row.encryptionPolicy?.effectiveEnabled ??
              isFolderEncryptionPolicyEnabled(row.metadata))
          ? options.labels.encryptedFolder
          : null;

    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 0.5,
          height: "100%",
          width: "100%",
        }}
      >
        {encryptionTitle && (
          <LockOutlined
            fontSize="small"
            titleAccess={encryptionTitle}
            sx={{ color: "text.secondary", flexShrink: 0 }}
          />
        )}
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
