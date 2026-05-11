import { Box } from "@mui/material";
import { forwardRef, type ComponentPropsWithoutRef } from "react";

export const SearchResultsScroller = forwardRef<
  HTMLDivElement,
  ComponentPropsWithoutRef<"div">
>((props, ref) => (
  <Box
    ref={ref}
    {...props}
    sx={{
      overflowX: "hidden",
      scrollbarWidth: "thin",
      "&::-webkit-scrollbar": {
        width: 8,
      },
      "&::-webkit-scrollbar-track": {
        bgcolor: "transparent",
      },
      "&::-webkit-scrollbar-thumb": {
        bgcolor: "action.disabled",
        borderRadius: 1,
      },
      "&::-webkit-scrollbar-thumb:hover": {
        bgcolor: "action.active",
      },
    }}
  />
));

SearchResultsScroller.displayName = "SearchResultsScroller";
