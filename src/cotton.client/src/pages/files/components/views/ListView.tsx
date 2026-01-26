import React, { useMemo } from "react";
import { Box } from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
import type { GridRowParams, GridRowsProp, GridColumnResizeParams } from "@mui/x-data-grid";
import { useTranslation } from "react-i18next";
import { isImageFile, isVideoFile } from "../../utils/fileTypes";
import type { IFileListView } from "../../types/FileListViewTypes";
import { createFileListColumns, type FileListRow } from "./fileListColumns";
import { usePreferencesStore } from "../../../../shared/store/preferencesStore";
import Loader from "../../../../shared/ui/Loader";

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
  autoHeight = false,
  loading = false,
}) => {
  const { t } = useTranslation("files");
  const [failedPreviews, setFailedPreviews] = React.useState<Set<string>>(
    new Set(),
  );
  const { layoutPreferences, setFileListColumnWidth } = usePreferencesStore();

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

  const columns = useMemo(
    () => {
      const cols = createFileListColumns({
        t,
        newFolderName,
        onNewFolderNameChange,
        onConfirmNewFolder,
        onCancelNewFolder,
        folderNamePlaceholder,
        fileNamePlaceholder,
        folderOperations,
        fileOperations,
        failedPreviews,
        setFailedPreviews,
      });

      // Apply saved column widths
      const savedWidths = layoutPreferences.fileListColumnWidths;
      if (savedWidths) {
        return cols.map((col) => {
          if (col.field && savedWidths[col.field]) {
            return { ...col, width: savedWidths[col.field] };
          }
          return col;
        });
      }

      return cols;
    },
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
      failedPreviews,
      layoutPreferences.fileListColumnWidths,
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
        fileOperations.onClick(row.id, row.name, row.sizeBytes ?? undefined);
      }
    }
  };

  const handleColumnResize = (params: GridColumnResizeParams) => {
    if (params.colDef.field) {
      setFileListColumnWidth(params.colDef.field, params.width);
    }
  };

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        minHeight: 0,
        position: "relative",
      }}
    >
      {loading && (
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
            bgcolor: "background.default",
            zIndex: 10,
          }}
        >
          <Loader />
        </Box>
      )}
      <DataGrid
        sx={{ height: "100%" }}
        rows={rows}
        columns={columns}
        disableRowSelectionOnClick
        onRowClick={handleRowClick}
        onColumnResize={handleColumnResize}
        hideFooter={!pagination}
        paginationMode={pagination ? "server" : "client"}
        autoPageSize={true}
        onPaginationModelChange={pagination?.onPaginationModelChange}
        rowCount={pagination ? pagination.totalCount : rows.length}
        loading={pagination?.loading}
        autoHeight={autoHeight}
      />
    </Box>
  );
};
