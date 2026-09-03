import {
  Close,
  DragIndicator,
  KeyboardArrowDown,
  KeyboardArrowUp,
} from "@mui/icons-material";
import {
  Card,
  CardContent,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { DashboardWidgetId } from "../dashboardModel";

interface DashboardWidgetFrameProps {
  children: ReactNode;
  compact?: boolean;
  customizing: boolean;
  first: boolean;
  last: boolean;
  onDragEnd: () => void;
  onDragStart: (widgetId: DashboardWidgetId) => void;
  onDrop: (widgetId: DashboardWidgetId) => void;
  onHide: (widgetId: DashboardWidgetId) => void;
  onMove: (widgetId: DashboardWidgetId, offset: -1 | 1) => void;
  title: string;
  widgetId: DashboardWidgetId;
}

export const DashboardWidgetFrame = ({
  children,
  compact = false,
  customizing,
  first,
  last,
  onDragEnd,
  onDragStart,
  onDrop,
  onHide,
  onMove,
  title,
  widgetId,
}: DashboardWidgetFrameProps) => {
  const { t } = useTranslation("home");

  return (
    <Card
      draggable={customizing}
      onDragStart={() => onDragStart(widgetId)}
      onDragEnd={onDragEnd}
      onDragOver={(event) => {
        if (customizing) {
          event.preventDefault();
        }
      }}
      onDrop={(event) => {
        event.preventDefault();
        onDrop(widgetId);
      }}
      sx={{
        minWidth: 0,
        gridColumn: {
          xs: "1 / -1",
          md: compact ? "span 4" : "span 8",
        },
        cursor: customizing ? "grab" : "default",
      }}
    >
      <CardContent>
        <Stack direction="row" alignItems="center" mb={1} gap={0.5}>
          <Typography variant="overline" color="text.secondary" flex={1}>
            {title}
          </Typography>
          {customizing && (
            <>
              <DragIndicator color="action" fontSize="small" />
              <IconButton
                size="small"
                aria-label={t("dashboard.actions.moveUp")}
                disabled={first}
                onClick={() => onMove(widgetId, -1)}
              >
                <KeyboardArrowUp fontSize="small" />
              </IconButton>
              <IconButton
                size="small"
                aria-label={t("dashboard.actions.moveDown")}
                disabled={last}
                onClick={() => onMove(widgetId, 1)}
              >
                <KeyboardArrowDown fontSize="small" />
              </IconButton>
              <IconButton
                size="small"
                aria-label={t("dashboard.actions.hide")}
                onClick={() => onHide(widgetId)}
              >
                <Close fontSize="small" />
              </IconButton>
            </>
          )}
        </Stack>
        {children}
      </CardContent>
    </Card>
  );
};
