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
    <Box
      width="100%"
      display="flex"
      flexDirection="column"
      flex={1}
      minHeight={0}
      py={3}
    >
      <Paper sx={{ display: { xs: "block", md: "none" }, p: 2, mb: 2 }}>
        <FormControl fullWidth size="small">
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
      </Paper>

      <Box
        display="grid"
        flex={1}
        sx={{
          gridTemplateColumns: { xs: "1fr", md: "220px 1fr" },
          gap: 2,
        }}
      >
        <Paper sx={{ display: { xs: "none", md: "block" } }}>
          <Typography variant="h6" fontWeight={700} p={2} pb={1}>
            {t("title")}
          </Typography>
          <List disablePadding>
            {items.map((item) => (
              <ListItemButton
                key={item.id}
                selected={isActive(item.to)}
                onClick={() => navigate(item.to)}
              >
                <ListItemText
                  primary={item.title}
                  slotProps={{ primary: { noWrap: true } }}
                />
              </ListItemButton>
            ))}
          </List>
        </Paper>

        <Box overflow="hidden">
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
};
