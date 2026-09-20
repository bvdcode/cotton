import type { ReactNode } from "react";
import { Box, CircularProgress, Typography } from "@mui/material";
import { Virtuoso } from "react-virtuoso";
import { SearchResultsScroller } from "./SearchResultsScroller";
import type { SearchRow } from "../types";

interface SearchResultsContentProps {
  emptyText: string;
  loadNextPage: () => void;
  loadingMore: boolean;
  renderSearchRow: (index: number, row: SearchRow) => ReactNode;
  rows: SearchRow[];
  waitingForResults: boolean;
}

export const SearchResultsContent = ({
  emptyText,
  loadNextPage,
  loadingMore,
  renderSearchRow,
  rows,
  waitingForResults,
}: SearchResultsContentProps) => {
  if (waitingForResults) {
    return (
      <Box
        sx={{
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (rows.length === 0) {
    return (
      <Box
        sx={{
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          px: 3,
          textAlign: "center",
        }}
      >
        <Typography color="text.secondary">{emptyText}</Typography>
      </Box>
    );
  }

  return (
    <Virtuoso
      style={{ height: "100%" }}
      data={rows}
      overscan={600}
      defaultItemHeight={68}
      components={{
        Scroller: SearchResultsScroller,
        Footer: () =>
          loadingMore ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 1.5 }}>
              <CircularProgress size={18} />
            </Box>
          ) : null,
      }}
      computeItemKey={(_, row) => row.id}
      endReached={loadNextPage}
      itemContent={renderSearchRow}
    />
  );
};
