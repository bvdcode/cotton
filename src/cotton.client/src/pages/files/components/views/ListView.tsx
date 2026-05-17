import React, { useCallback, useMemo, useRef, useState } from "react";
import { Box } from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
import type {
  GridRowClassNameParams,
  GridRowParams,
  GridRowsProp,
  GridRowSelectionModel,
} from "@mui/x-data-grid";
import { useTranslation } from "react-i18next";
import { getFileTypeInfo } from "../../utils/fileTypes";
import type { IFileListView } from "../../types/FileListViewTypes";
import { createFileListColumns, type FileListRow } from "./fileListColumns";
import Loader from "../../../../shared/ui/Loader";
import {
  isMoveDrag,
  moveDragHasSourceParent,
  moveDragHasItem,
  writeMoveDragPayload,
  readMoveDragPayload,
  filterMoveItemsForTarget,
} from "../../../../shared/hooks/useMoveOperations";
import type { MoveClipboardItem } from "../../../../shared/store/moveClipboardStore";

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

  const rows: GridRowsProp<FileListRow> = useMemo(() => {
    const baseRows: FileListRow[] = tiles.map((tile) => {
      if (tile.kind === "folder") {
        return {
          id: tile.node.id,
          type: "folder",
          name: tile.node.name,
          location: tile.path ?? null,
          sizeBytes: null,
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

  const orderedIds = useMemo(
    () =>
      rows
        .filter((r) => r.type !== "new-folder")
        .map((r) => String(r.id)),
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

  const containerRef = useRef<HTMLDivElement | null>(null);
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);

  const findRowElement = useCallback(
    (target: EventTarget | null): HTMLElement | null => {
      if (!(target instanceof Element)) return null;
      return target.closest<HTMLElement>("[data-id]");
    },
    [],
  );

  const buildDragPayloadForRow = useCallback(
    (rowId: string): ReadonlyArray<MoveClipboardItem> | null => {
      if (!moveSupport) return null;
      const currentParentId = moveSupport.currentParentId;
      if (!currentParentId) return null;

      const row = rowsById.get(rowId);
      if (!row) return null;

      const rowToItem = (r: FileListRow): MoveClipboardItem | null => {
        if (r.type === "folder") {
          return {
            id: String(r.id),
            kind: "folder",
            sourceParentId: currentParentId,
          };
        }
        if (r.type === "file") {
          return {
            id: String(r.id),
            kind: "file",
            sourceParentId: r.containerNodeId ?? currentParentId,
          };
        }
        return null;
      };

      const usingSelection =
        selectionMode &&
        selectedIds &&
        selectedIds.size > 1 &&
        selectedIds.has(rowId);

      if (usingSelection) {
        const items: MoveClipboardItem[] = [];
        for (const id of selectedIds!) {
          const candidate = rowsById.get(id);
          if (!candidate) continue;
          const item = rowToItem(candidate);
          if (item) items.push(item);
        }
        if (items.length > 0) return items;
      }

      const item = rowToItem(row);
      return item ? [item] : null;
    },
    [moveSupport, rowsById, selectedIds, selectionMode],
  );

  const handleContainerMouseDown = useCallback(
    (event: React.MouseEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (event.button !== 0) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) return;
      // Make rows draggable on demand so non-row interactions (text selection,
      // sort headers, checkboxes) keep working normally.
      rowEl.setAttribute("draggable", "true");
    },
    [findRowElement, moveSupport],
  );

  const handleContainerDragStart = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) return;
      const rowId = rowEl.getAttribute("data-id");
      if (!rowId) return;

      const items = buildDragPayloadForRow(rowId);
      if (!items || items.length === 0) {
        event.preventDefault();
        return;
      }
      writeMoveDragPayload(event.dataTransfer, { items });
    },
    [buildDragPayloadForRow, findRowElement, moveSupport],
  );

  const handleContainerDragOver = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (!isMoveDrag(event.dataTransfer)) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) {
        if (dropTargetId !== null) setDropTargetId(null);
        return;
      }
      const rowId = rowEl.getAttribute("data-id");
      if (!rowId) return;
      const row = rowsById.get(rowId);
      if (!row || row.type !== "folder") {
        if (dropTargetId !== null) setDropTargetId(null);
        return;
      }
      // Reject early: (a) folder cannot be a drop target for items already inside it,
      // (b) a folder cannot be dropped onto itself.
      if (moveDragHasSourceParent(event.dataTransfer, rowId)) return;
      if (moveDragHasItem(event.dataTransfer, rowId)) return;

      event.preventDefault();
      event.dataTransfer.dropEffect = "move";
      if (dropTargetId !== rowId) {
        setDropTargetId(rowId);
      }
    },
    [dropTargetId, findRowElement, moveSupport, rowsById],
  );

  const handleContainerDragLeave = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      const host = containerRef.current;
      if (!host) return;
      const related = event.relatedTarget as Node | null;
      if (related && host.contains(related)) return;
      setDropTargetId(null);
    },
    [moveSupport],
  );

  const handleContainerDrop = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      if (!moveSupport) return;
      if (!isMoveDrag(event.dataTransfer)) return;
      const rowEl = findRowElement(event.target);
      if (!rowEl) return;
      const rowId = rowEl.getAttribute("data-id");
      if (!rowId) return;
      const row = rowsById.get(rowId);
      if (!row || row.type !== "folder") return;

      event.preventDefault();
      event.stopPropagation();
      setDropTargetId(null);

      const payload = readMoveDragPayload(event.dataTransfer);
      if (!payload) return;
      const filtered = filterMoveItemsForTarget(payload.items, rowId);
      if (filtered.length === 0) return;
      moveSupport.onMove(filtered, rowId);
    },
    [findRowElement, moveSupport, rowsById],
  );

  const cutItemIds = moveSupport?.cutItemIds;

  const getRowClassName = useCallback(
    (params: GridRowClassNameParams<FileListRow>) => {
      const classes: string[] = [];
      const idStr = String(params.id);
      if (cutItemIds?.has(idStr)) classes.push("cotton-row-cut");
      if (dropTargetId === idStr) classes.push("cotton-row-drop");
      return classes.join(" ");
    },
    [cutItemIds, dropTargetId],
  );

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
          share: t("common:actions.share"),
          cut: t("move.cut"),
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
      const typeInfo = getFileTypeInfo(row.name, row.contentType ?? null);
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
        rowSelectionModel={selectionMode ? rowSelectionModel : { type: "include", ids: new Set() }}
        onRowSelectionModelChange={selectionMode ? handleRowSelectionModelChange : undefined}
        disableRowSelectionOnClick
        onRowClick={handleRowClick}
        hideFooter={false}
        paginationMode={pagination ? "server" : "client"}
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
