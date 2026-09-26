import React, { useMemo } from "react";
import { Box } from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
import type {
  GridRowParams,
  GridRowsProp,
  GridRowSelectionModel,
} from "@mui/x-data-grid";
import { useTranslation } from "react-i18next";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import type { IFileListView } from "@shared/types/FileListViewTypes";
import { createFileListColumns, type FileListRow } from "./fileListColumns";
import Loader from "../../../../shared/ui/Loader";
import { useListMoveDragAndDrop } from "./useListMoveDragAndDrop";

export const ListView: React.FC<IFileListView> = ({
  tiles,
  folderOperations,
  fileOperations,
  readOnly = false,
  onGoToFileLocation,
  listColumnFlex,
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
  selectionMode = false,
  selectedIds,
  onToggleItem,
  moveSupport,
}) => {
  const { t } = useTranslation("files");
  const [failedPreviews, setFailedPreviews] = React.useState<Set<string>>(
    new Set(),
  );
  const getFolderEncryptionPolicyState =
    folderOperations.getEncryptionPolicyState;

  const rows: GridRowsProp<FileListRow> = useMemo(() => {
    const baseRows: FileListRow[] = tiles.map((tile) => {
      if (tile.kind === "folder") {
        return {
          id: tile.node.id,
          type: "folder",
          name: tile.node.name,
          location: tile.path ?? null,
          sizeBytes: null,
          metadata: tile.node.metadata,
          encryptionPolicy: getFolderEncryptionPolicyState?.(tile.node),
          tile,
        };
      }

      return {
        id: tile.file.id,
        type: "file",
        name: tile.file.name,
        location: tile.path ?? null,
        containerPath: tile.containerPath ?? null,
        containerNodeId: tile.file.nodeId ?? null,
        sizeBytes: tile.file.sizeBytes,
        contentType: tile.file.contentType ?? null,
        metadata: "metadata" in tile.file ? tile.file.metadata : undefined,
        requiresVideoTranscoding: tile.file.requiresVideoTranscoding ?? false,
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
  }, [tiles, isCreatingFolder, newFolderName, getFolderEncryptionPolicyState]);

  const orderedIds = useMemo(
    () => rows.filter((r) => r.type !== "new-folder").map((r) => String(r.id)),
    [rows],
  );

  const rowsById = useMemo(() => {
    const map = new Map<string, FileListRow>();
    for (const row of rows) {
      if (row.type === "new-folder") continue;
      map.set(String(row.id), row);
    }
    return map;
  }, [rows]);

  const {
    containerRef,
    getRowClassName,
    handleContainerMouseDown,
    handleContainerDragStart,
    handleContainerDragOver,
    handleContainerDragLeave,
    handleContainerDrop,
  } = useListMoveDragAndDrop({
    rowsById,
    moveSupport,
    selectionMode,
    selectedIds,
    folderOperations,
    fileOperations,
  });

  const columns = useMemo(
    () =>
      createFileListColumns({
        readOnly,
        labels: {
          name: t("name"),
          size: t("size"),
          location: t("location"),
          actionsTitle: t("actionsTitle"),
          placeholder: t("common:placeholder"),
          goToFolder: t("actions.goToFolder"),
          rename: t("common:actions.rename"),
          delete: t("common:actions.delete"),
          restore: t("common:actions.restore"),
          download: t("common:actions.download"),
          versions: t("common:actions.versions"),
          share: t("common:actions.share"),
          cut: t("move.cut"),
          encryptedFile: t("common:clientEncryption.fileEncryptedHint"),
          encryptedFolder: t("common:clientEncryption.folderPolicyEnabledHint"),
          enableEncryptionPolicy: t("clientEncryption.enablePolicy"),
          disableEncryptionPolicy: t("clientEncryption.disablePolicy"),
          pin: t("home:dashboard.pinnedFolders.pin"),
          unpin: t("home:dashboard.pinnedFolders.unpin"),
        },
        newFolderName,
        onNewFolderNameChange,
        onConfirmNewFolder,
        onCancelNewFolder,
        folderNamePlaceholder,
        fileNamePlaceholder,
        folderOperations,
        fileOperations,
        onGoToFileLocation,
        columnFlex: listColumnFlex,
        failedPreviews,
        setFailedPreviews,
      }),
    [
      t,
      readOnly,
      newFolderName,
      onNewFolderNameChange,
      onConfirmNewFolder,
      onCancelNewFolder,
      folderNamePlaceholder,
      fileNamePlaceholder,
      folderOperations,
      fileOperations,
      onGoToFileLocation,
      listColumnFlex,
      failedPreviews,
    ],
  );

  const handleRowClick = (
    params: GridRowParams<FileListRow>,
    event: React.MouseEvent,
  ) => {
    const row = params.row;
    if (row.type === "new-folder") return;

    if (event.shiftKey && onToggleItem) {
      onToggleItem(row.id, {
        shiftKey: true,
        orderedIds,
      });
      return;
    }

    if (selectionMode) {
      onToggleItem?.(row.id, {
        shiftKey: event.shiftKey,
        orderedIds,
      });
      return;
    }

    if (row.type === "folder") {
      if (!folderOperations.isRenaming(row.id)) {
        folderOperations.onClick(row.id);
      }
      return;
    }

    if (!fileOperations.isRenaming(row.id)) {
      const typeInfo = getFileTypeInfo(row.name, row.contentType ?? null, {
        requiresVideoTranscoding: row.requiresVideoTranscoding ?? false,
      });
      if (typeInfo.type === "image" || typeInfo.type === "video") {
        fileOperations.onMediaClick?.(row.id);
      } else {
        fileOperations.onClick(row.id, row.name, row.sizeBytes ?? undefined);
      }
    }
  };

  const rowSelectionModel: GridRowSelectionModel = useMemo(
    () => ({
      type: "include" as const,
      ids: selectedIds ? new Set<string>(selectedIds) : new Set<string>(),
    }),
    [selectedIds],
  );

  const handleRowSelectionModelChange = (model: GridRowSelectionModel) => {
    if (!onToggleItem) return;
    const newIds = model.ids;
    const oldSet = selectedIds ?? new Set<string>();

    for (const id of newIds) {
      if (!oldSet.has(String(id))) onToggleItem(String(id));
    }
    for (const id of oldSet) {
      if (!newIds.has(id)) onToggleItem(id);
    }
  };

  return (
    <Box
      ref={containerRef}
      onMouseDown={moveSupport ? handleContainerMouseDown : undefined}
      onDragStart={moveSupport ? handleContainerDragStart : undefined}
      onDragOver={moveSupport ? handleContainerDragOver : undefined}
      onDragLeave={moveSupport ? handleContainerDragLeave : undefined}
      onDrop={moveSupport ? handleContainerDrop : undefined}
      sx={{
        width: "100%",
        height: autoHeight ? "auto" : "100%",
        minHeight: 0,
        position: "relative",
        "& .cotton-row-cut": { opacity: 0.45 },
        "& .cotton-row-drop": {
          outline: "2px solid",
          outlineColor: "primary.main",
          outlineOffset: -2,
          borderRadius: 1,
        },
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
        sx={{
          height: autoHeight ? "auto" : "100%",
          "& .MuiDataGrid-row.Mui-selected": {
            backgroundColor: "action.hover",
          },
          "& .MuiDataGrid-row.Mui-selected:hover": {
            backgroundColor: "action.selected",
          },
          "& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within": {
            outline: "none",
          },
        }}
        getRowClassName={moveSupport ? getRowClassName : undefined}
        rows={rows}
        columns={columns}
        checkboxSelection={selectionMode}
        rowSelectionModel={
          selectionMode
            ? rowSelectionModel
            : { type: "include", ids: new Set() }
        }
        onRowSelectionModelChange={
          selectionMode ? handleRowSelectionModelChange : undefined
        }
        disableRowSelectionOnClick
        onRowClick={handleRowClick}
        hideFooter={false}
        paginationMode={pagination ? "server" : "client"}
        paginationModel={pagination?.model}
        initialState={{
          pagination: {
            paginationModel: { page: 0, pageSize: 100 },
          },
        }}
        onPaginationModelChange={(model) => {
          if (!pagination) return;
          pagination.onPaginationModelChange({
            page: model.page,
            pageSize: Math.min(100, model.pageSize),
          });
        }}
        pageSizeOptions={[100]}
        rowCount={pagination ? pagination.totalCount : rows.length}
        loading={pagination?.loading}
        autoHeight={autoHeight}
      />
    </Box>
  );
};
