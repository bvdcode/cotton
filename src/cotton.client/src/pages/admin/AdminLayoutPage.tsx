import {
  Box,
  FormControl,
  InputLabel,
  List,
  ListItemButton,
  ListItemText,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import type { SelectChangeEvent } from "@mui/material/Select";
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

  const selectedTo: string =
    items.find((x) => location.pathname.startsWith(x.to))?.to ?? items[0].to;

  const handleMobileNavigate = (event: SelectChangeEvent<string>) => {
    navigate(event.target.value);
  };

  return (
    <Box p={{ xs: 2, sm: 3 }} display="flex" flex={1} minHeight={0}>
      <Stack spacing={2} flex={1} minHeight={0}>
        <Paper sx={{ display: { xs: "block", md: "none" } }}>
          <Box p={2}>
            <FormControl fullWidth>
              <InputLabel id="admin-menu-navigate-label">
                {t("menu.navigate")}
              </InputLabel>
              <Select
                labelId="admin-menu-navigate-label"
                value={selectedTo}
                label={t("menu.navigate")}
                onChange={handleMobileNavigate}
              >
                {items.map((item) => (
                  <MenuItem key={item.id} value={item.to}>
                    {item.title}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Box>
        </Paper>

        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={2}
          alignItems="stretch"
          flex={1}
          minHeight={0}
        >
          <Paper
            sx={{
              flexShrink: 0,
              display: { xs: "none", md: "block" },
              overflow: "hidden",
              minWidth: 220,
              maxWidth: 280,
            }}
          >
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
                  <ListItemText
                    primary={item.title}
                    primaryTypographyProps={{ noWrap: true }}
                  />
                </ListItemButton>
              ))}
            </List>
          </Paper>

          <Box flex={1} minWidth={0} minHeight={0}>
            <Outlet />
          </Box>
        </Stack>
      </Stack>
    </Box>
  );
};
