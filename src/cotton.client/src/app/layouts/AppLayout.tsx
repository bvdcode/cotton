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
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { useEffect } from "react";
import { useAuth } from "../../features/auth";
import { useSettingsStore } from "../../shared/store/settingsStore";

interface AppLayoutProps {
  routes: RouteConfig[];
}

export const AppLayout = ({ routes }: AppLayoutProps) => {
  const location = useLocation();
  const { isAuthenticated } = useAuth();
  const settingsLoaded = useSettingsStore((s) => s.loaded);
  const settingsLoading = useSettingsStore((s) => s.loading);
  const fetchSettings = useSettingsStore((s) => s.fetchSettings);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }
    if (settingsLoaded || settingsLoading) {
      return;
    }
    fetchSettings();
  }, [isAuthenticated, settingsLoaded, settingsLoading, fetchSettings]);

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

              if (isMobile) {
                return (
                  <IconButton
                    key={route.path}
                    component={Link}
                    to={route.path}
                    color="inherit"
                    sx={{
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
                );
              }

              return (
                <Button
                  key={route.path}
                  component={Link}
                  to={route.path}
                  startIcon={route.icon}
                  sx={{
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
              );
            })}
          </Box>

          <UserMenu />
        </Toolbar>
      </AppBar>

      <Container
        component="main"
        maxWidth={false}
        sx={{
          pt: 0,
          pb: 2,
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
