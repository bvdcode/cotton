import {
  Box,
  Button,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { ClearAll, Close, History } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { SearchHistoryEntry } from "../utils/searchHistory";

interface SearchHistoryPanelProps {
  entries: SearchHistoryEntry[];
  onSelect: (query: string) => void;
  onRemove: (query: string) => void;
  onClear: () => void;
  compact?: boolean;
}

export const SearchHistoryPanel = ({
  entries,
  onSelect,
  onRemove,
  onClear,
  compact = false,
}: SearchHistoryPanelProps) => {
  const { t } = useTranslation("search");

  if (entries.length === 0) {
    return null;
  }

  return (
    <Box sx={{ minWidth: 0 }}>
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
        gap={1}
        sx={{ px: compact ? 0.5 : 1, py: compact ? 0.5 : 1 }}
      >
        <Typography variant="subtitle2" color="text.secondary">
          {t("history.title")}
        </Typography>
        <Button
          size="small"
          startIcon={<ClearAll fontSize="small" />}
          onClick={onClear}
          sx={{ flexShrink: 0 }}
        >
          {t("history.clear")}
        </Button>
      </Stack>

      <List disablePadding>
        {entries.map((entry, index) => {
          const isLast = index === entries.length - 1;

          return (
            <ListItem
              key={entry.query}
              disablePadding
              secondaryAction={
                <Tooltip title={t("history.remove")}>
                  <IconButton
                    edge="end"
                    aria-label={t("history.removeQuery", {
                      query: entry.query,
                    })}
                    onClick={() => onRemove(entry.query)}
                  >
                    <Close fontSize="small" />
                  </IconButton>
                </Tooltip>
              }
              sx={{
                borderBottom: isLast ? 0 : 1,
                borderColor: "divider",
              }}
            >
              <ListItemButton
                onClick={() => onSelect(entry.query)}
                sx={{
                  minHeight: compact ? 44 : 52,
                  pr: 7,
                  borderRadius: 0.75,
                }}
              >
                <ListItemIcon sx={{ minWidth: compact ? 36 : 42 }}>
                  <History fontSize="small" color="action" />
                </ListItemIcon>
                <ListItemText
                  primary={entry.query}
                  primaryTypographyProps={{
                    noWrap: true,
                    sx: { fontWeight: 500 },
                  }}
                />
              </ListItemButton>
            </ListItem>
          );
        })}
      </List>
    </Box>
  );
};
