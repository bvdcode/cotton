import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import {
  Box,
  Divider,
  FormControl,
  IconButton,
  InputLabel,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  MenuItem,
  Paper,
  Select,
  Tooltip,
} from "@mui/material";
import type { SelectChangeEvent } from "@mui/material/Select";
import { NavLink } from "react-router-dom";
import type { AdminMenuItem, AdminMenuSection } from "./adminNavigationModel";

type AdminNavigationItemProps = {
  item: AdminMenuItem;
  expanded: boolean;
};

const AdminNavigationItem = ({ item, expanded }: AdminNavigationItemProps) => {
  const Icon = item.icon;
  const button = (
    <ListItemButton
      component={NavLink}
      to={item.to}
      aria-label={expanded ? undefined : item.title}
      sx={{
        position: "relative",
        justifyContent: expanded ? "flex-start" : "center",
        minHeight: 44,
        mx: 0.75,
        px: expanded ? 1.5 : 0.75,
        borderRadius: 0.5,
        color: "text.primary",
        "& .MuiListItemIcon-root": {
          color: "text.secondary",
        },
        "&[aria-current='page']": {
          color: "primary.main",
          bgcolor: "action.selected",
          "& .MuiListItemIcon-root": {
            color: "primary.main",
          },
          "&::before": {
            content: '""',
            position: "absolute",
            left: 0,
            top: "50%",
            width: 3,
            height: 24,
            transform: "translateY(-50%)",
            borderRadius: "0 2px 2px 0",
            bgcolor: "primary.main",
          },
        },
      }}
    >
      <ListItemIcon
        sx={{
          minWidth: expanded ? 40 : 0,
          justifyContent: "center",
        }}
      >
        <Icon />
      </ListItemIcon>
      {expanded && <ListItemText primary={item.title} />}
    </ListItemButton>
  );

  return expanded ? (
    button
  ) : (
    <Tooltip title={item.title} placement="right">
      {button}
    </Tooltip>
  );
};

interface DesktopAdminNavigationProps {
  sections: AdminMenuSection[];
  expanded: boolean;
  onToggle: () => void;
  navigationLabel: string;
  toggleLabel: string;
}

export const DesktopAdminNavigation = ({
  sections,
  expanded,
  onToggle,
  navigationLabel,
  toggleLabel,
}: DesktopAdminNavigationProps) => (
  <Paper
    component="aside"
    sx={{
      display: { xs: "none", md: "flex" },
      flexDirection: "column",
      alignSelf: "stretch",
      minHeight: 0,
      overflow: "hidden",
    }}
  >
    <Box component="nav" aria-label={navigationLabel} overflow="auto" flex={1}>
      <List sx={{ py: 1 }}>
        {sections.map((section, sectionIndex) => (
          <Box component="li" key={section.id} sx={{ listStyle: "none" }}>
            {expanded && (
              <ListSubheader
                component="div"
                disableSticky
                sx={{
                  bgcolor: "transparent",
                  lineHeight: 2.5,
                  textTransform: "uppercase",
                }}
              >
                {section.title}
              </ListSubheader>
            )}
            {section.items.map((item) => (
              <AdminNavigationItem
                key={item.id}
                item={item}
                expanded={expanded}
              />
            ))}
            {sectionIndex < sections.length - 1 && (
              <Divider sx={{ mx: 1.5, my: 0.5 }} />
            )}
          </Box>
        ))}
      </List>
    </Box>
    <Divider />
    <Box
      display="flex"
      justifyContent={expanded ? "flex-end" : "center"}
      p={0.5}
    >
      <IconButton aria-label={toggleLabel} onClick={onToggle}>
        {expanded ? <ChevronLeftIcon /> : <ChevronRightIcon />}
      </IconButton>
    </Box>
  </Paper>
);

interface MobileAdminNavigationProps {
  sections: AdminMenuSection[];
  selectedTo: string;
  label: string;
  onChange: (event: SelectChangeEvent<string>) => void;
}

export const MobileAdminNavigation = ({
  sections,
  selectedTo,
  label,
  onChange,
}: MobileAdminNavigationProps) => (
  <FormControl
    fullWidth
    size="small"
    sx={{ display: { xs: "flex", md: "none" }, mb: 2 }}
  >
    <InputLabel id="admin-menu-navigate-label">{label}</InputLabel>
    <Select
      labelId="admin-menu-navigate-label"
      label={label}
      value={selectedTo}
      onChange={onChange}
    >
      {sections.flatMap((section) => [
        <ListSubheader key={`${section.id}-header`}>
          {section.title}
        </ListSubheader>,
        ...section.items.map((item) => {
          const Icon = item.icon;
          return (
            <MenuItem key={item.id} value={item.to}>
              <ListItemIcon sx={{ minWidth: 36 }}>
                <Icon fontSize="small" />
              </ListItemIcon>
              <ListItemText primary={item.title} />
            </MenuItem>
          );
        }),
      ])}
    </Select>
  </FormControl>
);
