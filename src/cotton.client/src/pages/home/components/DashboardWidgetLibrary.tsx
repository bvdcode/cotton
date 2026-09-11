import { Add } from "@mui/icons-material";
import { Button, Divider, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { DashboardWidgetId } from "../dashboardModel";
import { getDashboardWidgetTitle } from "../dashboardWidgetMetadata";

interface DashboardWidgetLibraryProps {
  hiddenWidgetIds: readonly DashboardWidgetId[];
  onRestore: (widgetId: DashboardWidgetId) => void;
}

export const DashboardWidgetLibrary = ({
  hiddenWidgetIds,
  onRestore,
}: DashboardWidgetLibraryProps) => {
  const { t } = useTranslation("home");

  return (
    <Stack mt={3} gap={1}>
      <Divider />
      <Typography variant="subtitle1" mt={1}>
        {t("dashboard.library.title")}
      </Typography>
      {hiddenWidgetIds.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("dashboard.library.empty")}
        </Typography>
      ) : (
        <Stack direction="row" flexWrap="wrap" gap={1}>
          {hiddenWidgetIds.map((widgetId) => (
            <Button
              key={widgetId}
              variant="outlined"
              startIcon={<Add />}
              onClick={() => onRestore(widgetId)}
            >
              {getDashboardWidgetTitle(t, widgetId)}
            </Button>
          ))}
        </Stack>
      )}
    </Stack>
  );
};
