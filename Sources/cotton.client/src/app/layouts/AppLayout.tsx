import type { RouteConfig } from "../types";
import { UserMenu } from "./components/UserMenu";
import { Outlet, Link, useLocation } from "react-router-dom";
import { AppBar, Toolbar, Box, Button, Container } from "@mui/material";

interface AppLayoutProps {
  routes: RouteConfig[];
}

export const AppLayout = ({ routes }: AppLayoutProps) => {
  const location = useLocation();

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
      <AppBar position="static" elevation={1}>
        <Toolbar
          disableGutters
          sx={{
            px: { xs: 2, sm: 2 },
          }}
        >
          <Box sx={{ display: "flex", gap: 1, flexGrow: 1 }}>
            {routes.map((route) => {
              const isActive = location.pathname === route.path;
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
          py: 2,
          px: { xs: 2, sm: 0 },
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
    </Box>
  );
};
