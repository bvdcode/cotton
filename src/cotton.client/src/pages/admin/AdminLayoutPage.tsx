import { Box, List, ListItemButton, ListItemText, Paper, Stack, Typography } from "@mui/material";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

type AdminMenuItem = {
  id: "users";
  to: string;
  title: string;
};

export const AdminLayoutPage = () => {
  const { t } = useTranslation("admin");
  const navigate = useNavigate();
  const location = useLocation();

  const items: AdminMenuItem[] = [
    {
      id: "users",
      to: "/admin/users",
      title: t("menu.users"),
    },
  ];

  const isActive = (to: string) => location.pathname === to;

  return (
    <Box p={{ xs: 2, sm: 3 }} display="flex" flex={1} minHeight={0}>
      <Stack direction="row" spacing={2} alignItems="stretch" flex={1} minHeight={0}>
        <Paper sx={{ width: 280, flexShrink: 0, display: { xs: "none", md: "block" } }}>
          <Box p={2}>
            <Typography variant="h6" fontWeight={700}>
              {t("title")}
            </Typography>
          </Box>
          <List disablePadding>
            {items.map((item) => (
              <ListItemButton
                key={item.id}
                selected={isActive(item.to)}
                onClick={() => navigate(item.to)}
              >
                <ListItemText primary={item.title} />
              </ListItemButton>
            ))}
          </List>
        </Paper>

        <Box flex={1} minWidth={0} minHeight={0}>
          <Outlet />
        </Box>
      </Stack>
    </Box>
  );
};
