import type { RouteConfig } from "../types";
import { UserMenu } from "./components/UserMenu";
import { NotificationsMenu } from "./components/NotificationsMenu";
import { UploadFilePicker } from "./components/UploadFilePicker";
import { UploadQueueWidget } from "./components/UploadQueueWidget";
import { Outlet, Link, useLocation } from "react-router-dom";
import {
  AppBar,
  Toolbar,
  Box,
  Button,
  Container,
  IconButton,
  Tooltip,
} from "@mui/material";
import { Search as SearchIcon } from "@mui/icons-material";
import React, { useEffect } from "react";
import { alpha, useTheme } from "@mui/material/styles";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../features/auth";
import { OPEN_SEARCH_EVENT, SearchModal } from "../../features/search";
import { useSettingsStore } from "../../shared/store/settingsStore";
import { AudioPlayerBar } from "../components/AudioPlayerBar";
import { ErrorBoundary } from "../../shared/ui/ErrorBoundary";

interface AppLayoutProps {
  routes: RouteConfig[];
}

export const AppLayout = ({ routes }: AppLayoutProps) => {
  const location = useLocation();
  const { t } = useTranslation("routes");
  const { t: tSearch } = useTranslation("search");
  const { isAuthenticated } = useAuth();
  const theme = useTheme();
  const settingsLoaded = useSettingsStore((s) => s.loaded);
  const settingsLoading = useSettingsStore((s) => s.loading);
  const settingsError = useSettingsStore((s) => s.error);
  const fetchSettings = useSettingsStore((s) => s.fetchSettings);
  const [searchOpen, setSearchOpen] = React.useState(false);

  const navTextColor = theme.palette.text.primary;
  const navActiveBg = alpha(navTextColor, 0.14);
  const navHoverBg = alpha(navTextColor, 0.2);

  const openSearch = React.useCallback(() => {
    setSearchOpen(true);
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }
    if (settingsLoaded || settingsLoading || settingsError) {
      return;
    }
    fetchSettings();
  }, [
    isAuthenticated,
    settingsLoaded,
    settingsLoading,
    settingsError,
    fetchSettings,
  ]);

  useEffect(() => {
    const handleOpenSearch = () => openSearch();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey)) return;
      if (event.key.toLocaleLowerCase() !== "f") return;

      event.preventDefault();
      openSearch();
    };

    window.addEventListener(OPEN_SEARCH_EVENT, handleOpenSearch);
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      window.removeEventListener(OPEN_SEARCH_EVENT, handleOpenSearch);
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [openSearch]);

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden",
      }}
    >
      <AppBar
        position="static"
        elevation={1}
        sx={{
          boxShadow: "none",
          color: navTextColor,
        }}
      >
        <Toolbar
          disableGutters
          sx={{
            px: { xs: 1, sm: 2 },
            gap: { xs: 0.5, sm: 1 },
          }}
        >
          <Box
            sx={{
              display: "flex",
              gap: { xs: 0.5, sm: 1 },
              flexGrow: 1,
              overflow: "auto",
              scrollbarWidth: "none",
              "&::-webkit-scrollbar": { display: "none" },
            }}
          >
            {routes.map((route) => {
              const isActive =
                route.path === "/"
                  ? location.pathname === route.path
                  : location.pathname.startsWith(route.path);

              return (
                <React.Fragment key={route.path}>
                  <IconButton
                    key={`${route.path}-icon`}
                    component={Link}
                    to={route.path}
                    color="inherit"
                    sx={{
                      display: { xs: "inline-flex", sm: "none" },
                      bgcolor: isActive ? navActiveBg : "transparent",
                      "&:hover": {
                        bgcolor: navHoverBg,
                      },
                    }}
                  >
                    {route.icon}
                  </IconButton>
                  <Button
                    key={`${route.path}-btn`}
                    component={Link}
                    to={route.path}
                    startIcon={route.icon}
                    sx={{
                      display: { xs: "none", sm: "inline-flex" },
                      color: "inherit",
                      bgcolor: isActive ? navActiveBg : "transparent",
                      "&:hover": {
                        bgcolor: navHoverBg,
                      },
                    }}
                  >
                    {route.translationKey
                      ? t(route.translationKey)
                      : route.displayName}
                  </Button>
                </React.Fragment>
              );
            })}
          </Box>

          <Tooltip title={tSearch("open")}>
            <IconButton aria-label={tSearch("open")} onClick={openSearch}>
              <SearchIcon />
            </IconButton>
          </Tooltip>
          <NotificationsMenu />
          <UserMenu />
        </Toolbar>
      </AppBar>

      <Container
        component="main"
        maxWidth={false}
        sx={(theme) => ({
          pt: 0,
          pb: {
            xs: `calc(${theme.spacing(1)} + var(--audio-player-bar-offset, 0px))`,
            sm: 1,
          },
          px: { xs: 1, sm: 1 },
          flexGrow: 1,
          minHeight: 0,
          scrollbarGutter: "stable both-edges",
          overflow: "auto",
          display: "flex",
          flexDirection: "column",
        })}
      >
        <ErrorBoundary>
          <Outlet />
        </ErrorBoundary>
      </Container>

      <AudioPlayerBar />

      <UploadFilePicker />
      <UploadQueueWidget />
      <SearchModal open={searchOpen} onClose={() => setSearchOpen(false)} />
    </Box>
  );
};
