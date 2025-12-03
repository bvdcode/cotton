import { isValidElement } from "react";
import type { ReactKitPage } from "../types";
import { Box, Tabs, Tab, Tooltip } from "@mui/material";

export type NavTabsProps = {
  pages: ReactKitPage[];
  currentPath: string;
  onNavigate: (route: string) => void;
};

export default function NavTabs({
  pages,
  currentPath,
  onNavigate,
}: NavTabsProps) {
  const displayedPages = pages.filter(
    (p) => p.name !== undefined || isValidElement(p.icon),
  );

  const currentIndex = displayedPages.findIndex((p) => p.route === currentPath);
  if (!displayedPages || displayedPages.length <= 1) {
    return <Box sx={{ minWidth: 0 }} />;
  }

  return (
    <Box sx={{ display: "flex", justifyContent: "center", minWidth: 0 }}>
      <Tabs
        value={currentIndex !== -1 ? currentIndex : false}
        textColor="inherit"
        indicatorColor="secondary"
        variant="scrollable"
        scrollButtons="auto"
        onChange={(_, value) => {
          if (value !== false) {
            const page = displayedPages[value];
            onNavigate(page.url ?? page.route);
          }
        }}
      >
        {displayedPages.map((page) => {
          const iconEl = isValidElement(page.icon) ? page.icon : undefined;
          const labelText = page.name || page.route;

          return (
            <Tab
              key={page.route}
              aria-label={labelText}
              label={!iconEl ? labelText : undefined}
              icon={
                iconEl && (
                  <Tooltip title={labelText} arrow>
                    <Box
                      component="span"
                      sx={{ cursor: "pointer", display: "inline-flex" }}
                    >
                      {iconEl}
                    </Box>
                  </Tooltip>
                )
              }
              sx={{ minWidth: 40, px: 1, cursor: "pointer" }}
            />
          );
        })}
      </Tabs>
    </Box>
  );
}
