import { useEffect, useMemo, useRef, useState } from "react";
import { Alert, Box, Tab, Tabs } from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { useTranslation } from "react-i18next";
import { useDataGridLocaleText } from "../../../hooks/useDataGridLocaleText";
import { reportClientError } from "../../../utils/clientDiagnostics";
import type {
  SpreadsheetPage,
  SpreadsheetRequest,
  SpreadsheetResponse,
} from "./spreadsheetPreviewTypes";

export default function SpreadsheetPreview({ bytes }: { bytes: ArrayBuffer }) {
  const { t } = useTranslation("files");
  const localeText = useDataGridLocaleText();
  const workerRef = useRef<Worker | null>(null);
  const selectedRef = useRef(0);
  const [page, setPage] = useState<SpreadsheetPage>();
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    const worker = new Worker(
      new URL("./spreadsheet.worker.ts", import.meta.url),
      { type: "module" },
    );
    workerRef.current = worker;
    worker.onmessage = (event: MessageEvent<SpreadsheetResponse>) => {
      if ("page" in event.data) {
        if (event.data.page.sheetIndex !== selectedRef.current) {
          return;
        }
        setPage(event.data.page);
      } else {
        reportClientError(
          "Failed to parse spreadsheet",
          new Error("Unsupported or damaged workbook."),
        );
        setFailed(true);
      }
      setLoading(false);
    };
    worker.onerror = (error) => {
      reportClientError("Spreadsheet worker failed", error);
      setFailed(true);
      setLoading(false);
    };
    worker.postMessage({ bytes, sheetIndex: 0 } satisfies SpreadsheetRequest);
    return () => {
      worker.terminate();
      workerRef.current = null;
    };
  }, [bytes]);
  const columns = useMemo<GridColDef[]>(
    () => [
      { field: "id", headerName: "", width: 60, sortable: false },
      ...(page?.columns ?? []).map((name) => ({
        field: name,
        headerName: name,
        width: 140,
        sortable: false,
      })),
    ],
    [page?.columns],
  );
  const rows = useMemo(
    () =>
      page?.rows.map((row) => ({
        id: row.id,
        ...Object.fromEntries(
          row.cells.map((cell, index) => [page.columns[index], cell]),
        ),
      })) ?? [],
    [page],
  );
  if (failed) {
    return (
      <Alert severity="error">
        {t("preview.errors.loadFailed", { error: "" })}
      </Alert>
    );
  }
  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        flex: 1,
        minHeight: 0,
        minWidth: 0,
      }}
    >
      {page?.truncated && (
        <Alert severity="info">
          {t("preview.office.truncated", {
            rows: page.rows.length,
            columns: page.columns.length,
          })}
        </Alert>
      )}
      <DataGrid
        key={page?.sheetIndex ?? 0}
        rows={rows}
        columns={columns}
        loading={loading}
        localeText={localeText}
        density="compact"
        disableColumnMenu
        disableRowSelectionOnClick
        initialState={{ pagination: { paginationModel: { pageSize: 100 } } }}
        pageSizeOptions={[25, 50, 100]}
        sx={{ flex: 1, minHeight: 0, border: 0 }}
      />
      {page && (
        <Tabs
          value={page.sheetIndex}
          textColor="inherit"
          variant="scrollable"
          scrollButtons="auto"
          onChange={(_, index: number) => {
            selectedRef.current = index;
            setLoading(true);
            workerRef.current?.postMessage({
              sheetIndex: index,
            } satisfies SpreadsheetRequest);
          }}
        >
          {page.sheetNames.map((name, index) => (
            <Tab key={name} value={index} label={name} />
          ))}
        </Tabs>
      )}
    </Box>
  );
}
