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
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import {
  DASHBOARD_WIDGET_SIZES,
  type DashboardWidgetId,
  type DashboardWidgetSize,
} from "../dashboardModel";

interface DashboardWidgetFrameProps {
  children: ReactNode;
  customizing: boolean;
  first: boolean;
  last: boolean;
  onDragEnd: () => void;
  onDragStart: (widgetId: DashboardWidgetId) => void;
  onDrop: (widgetId: DashboardWidgetId) => void;
  onHide: (widgetId: DashboardWidgetId) => void;
  onMove: (widgetId: DashboardWidgetId, offset: -1 | 1) => void;
  onResize: (widgetId: DashboardWidgetId, size: DashboardWidgetSize) => void;
  size: DashboardWidgetSize;
  title: string;
  widgetId: DashboardWidgetId;
}

export const DashboardWidgetFrame = ({
  children,
  customizing,
  first,
  last,
  onDragEnd,
  onDragStart,
  onDrop,
  onHide,
  onMove,
  onResize,
  size,
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
          md: `span ${size * 4}`,
        },
        cursor: customizing ? "grab" : "default",
      }}
    >
      <CardContent>
        <Stack
          direction="row"
          alignItems="center"
          flexWrap="wrap"
          mb={1}
          gap={0.5}
        >
          <Typography variant="overline" color="text.secondary" flex={1}>
            {title}
          </Typography>
          {customizing && (
            <Stack
              direction="row"
              alignItems="center"
              gap={0.25}
              flexShrink={0}
              ml="auto"
            >
              <ToggleButtonGroup
                exclusive
                size="small"
                value={size}
                aria-label={title}
                onChange={(_event, nextSize: DashboardWidgetSize | null) => {
                  if (nextSize !== null) {
                    onResize(widgetId, nextSize);
                  }
                }}
                sx={{
                  height: 28,
                  "& .MuiToggleButton-root": {
                    minWidth: 28,
                    px: 0.75,
                    py: 0,
                  },
                }}
              >
                {DASHBOARD_WIDGET_SIZES.map((candidateSize) => (
                  <ToggleButton
                    key={candidateSize}
                    value={candidateSize}
                    aria-label={t("dashboard.actions.size", {
                      size: candidateSize,
                    })}
                    title={t("dashboard.actions.size", {
                      size: candidateSize,
                    })}
                  >
                    {candidateSize}
                  </ToggleButton>
                ))}
              </ToggleButtonGroup>
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
            </Stack>
          )}
        </Stack>
        {children}
      </CardContent>
    </Card>
  );
};
