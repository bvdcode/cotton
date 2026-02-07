import type { RouteConfig } from "../types";
import { UserMenu } from "./components/UserMenu";
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
} from "@mui/material";
import React, { useEffect } from "react";
import { useAuth } from "../../features/auth";
import { useSettingsStore } from "../../shared/store/settingsStore";
import { useNodesStore } from "../../shared/store/nodesStore";
import Loader from "../../shared/ui/Loader";

interface AppLayoutProps {
  routes: RouteConfig[];
}

export const AppLayout = ({ routes }: AppLayoutProps) => {
  const location = useLocation();
  const { isAuthenticated } = useAuth();
  const settingsLoaded = useSettingsStore((s) => s.loaded);
  const settingsLoading = useSettingsStore((s) => s.loading);
  const fetchSettings = useSettingsStore((s) => s.fetchSettings);
  const nodesLoading = useNodesStore((s) => s.loading);
  const [showOverlayLoader, setShowOverlayLoader] = React.useState(false);

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }
    if (settingsLoaded || settingsLoading) {
      return;
    }
    fetchSettings();
  }, [isAuthenticated, settingsLoaded, settingsLoading, fetchSettings]);

  useEffect(() => {
    if (!nodesLoading) {
      setShowOverlayLoader(false);
      return;
    }

    const timeoutId = window.setTimeout(() => {
      setShowOverlayLoader(true);
    }, 350);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [nodesLoading]);

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
                      bgcolor: isActive
                        ? "rgba(255, 255, 255, 0.1)"
                        : "transparent",
                      "&:hover": {
                        bgcolor: "rgba(255, 255, 255, 0.15)",
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
                      bgcolor: isActive
                        ? "rgba(255, 255, 255, 0.1)"
                        : "transparent",
                      "&:hover": {
                        bgcolor: "rgba(255, 255, 255, 0.15)",
                      },
                    }}
                  >
                    {route.displayName}
                  </Button>
                </React.Fragment>
              );
            })}
          </Box>

          <UserMenu />
        </Toolbar>
      </AppBar>

      {showOverlayLoader && <Loader overlay />}

      <Container
        component="main"
        maxWidth={false}
        sx={{
          pt: 0,
          pb: 1,
          px: { xs: 1, sm: 1 },
          flexGrow: 1,
          minHeight: 0,
          scrollbarGutter: "stable both-edges",
          overflow: "auto",
          display: "flex",
          flexDirection: "column",
        }}
      >
        <Outlet />
      </Container>

      <UploadFilePicker />
      <UploadQueueWidget />
    </Box>
  );
};
