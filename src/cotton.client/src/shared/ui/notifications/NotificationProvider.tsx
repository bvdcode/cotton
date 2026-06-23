import CheckCircleRoundedIcon from "@mui/icons-material/CheckCircleRounded";
import CloseRoundedIcon from "@mui/icons-material/CloseRounded";
import ErrorOutlineRoundedIcon from "@mui/icons-material/ErrorOutlineRounded";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import {
  Box,
  GlobalStyles,
  IconButton,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import {
  alpha,
  useTheme,
  type Palette,
  type PaletteColor,
} from "@mui/material/styles";
import {
  closeSnackbar,
  SnackbarContent,
  SnackbarProvider,
  type CustomContentProps,
  type VariantType,
} from "notistack";
import { forwardRef, type ReactNode } from "react";

interface NotificationProviderProps {
  children: ReactNode;
}

const NOTIFICATION_CONTAINER_CLASS = "cotton-notification-container";
const FOREGROUND_OVERLAY_Z_INDEX = 11000;

export function NotificationProvider({ children }: NotificationProviderProps) {
  return (
    <>
      <GlobalStyles
        styles={{
          [`.${NOTIFICATION_CONTAINER_CLASS}.${NOTIFICATION_CONTAINER_CLASS}`]:
            {
              zIndex: `${FOREGROUND_OVERLAY_Z_INDEX} !important`,
            },
        }}
      />
      <SnackbarProvider
        maxSnack={4}
        autoHideDuration={4500}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        preventDuplicate
        hideIconVariant
        classes={{ containerRoot: NOTIFICATION_CONTAINER_CLASS }}
        Components={notificationComponents}
      >
        {children}
      </SnackbarProvider>
    </>
  );
}

const ThemedNotificationContent = forwardRef<
  HTMLDivElement,
  CustomContentProps
>(function ThemedNotificationContent(
  { id, message, variant, action, className, style },
  ref,
) {
  const theme = useTheme();
  const tone = getTone(theme.palette, variant);
  const actionNode = typeof action === "function" ? action(id) : action;

  return (
    <SnackbarContent ref={ref} role="alert" className={className} style={style}>
      <Paper
        elevation={theme.palette.mode === "dark" ? 10 : 8}
        sx={{
          width: "100%",
          minWidth: { xs: "calc(100vw - 32px)", sm: 360 },
          maxWidth: { xs: "calc(100vw - 32px)", sm: 520 },
          borderRadius: 2,
          border: `1px solid ${alpha(tone.main, theme.palette.mode === "dark" ? 0.42 : 0.28)}`,
          bgcolor: theme.palette.background.paper,
          color: theme.palette.text.primary,
          overflow: "hidden",
        }}
      >
        <Stack
          direction="row"
          spacing={1.25}
          alignItems="center"
          sx={{
            px: 1.5,
            py: 1.25,
            boxShadow: `inset 4px 0 0 ${tone.main}`,
          }}
        >
          <Box
            sx={{
              display: "grid",
              placeItems: "center",
              width: 30,
              height: 30,
              flex: "0 0 auto",
              borderRadius: "50%",
              bgcolor: alpha(
                tone.main,
                theme.palette.mode === "dark" ? 0.18 : 0.12,
              ),
              color: tone.main,
            }}
          >
            {getIcon(variant)}
          </Box>
          <Typography
            component="div"
            variant="body2"
            sx={{
              flex: 1,
              minWidth: 0,
              color: "text.primary",
              overflowWrap: "anywhere",
            }}
          >
            {message}
          </Typography>
          {actionNode}
          <IconButton
            aria-label="Close notification"
            size="small"
            onClick={() => closeSnackbar(id)}
            sx={{
              color: "text.secondary",
              flex: "0 0 auto",
              "&:hover": {
                bgcolor: alpha(tone.main, 0.1),
                color: tone.main,
              },
            }}
          >
            <CloseRoundedIcon fontSize="small" />
          </IconButton>
        </Stack>
      </Paper>
    </SnackbarContent>
  );
});

const notificationComponents = {
  default: ThemedNotificationContent,
  success: ThemedNotificationContent,
  error: ThemedNotificationContent,
  warning: ThemedNotificationContent,
  info: ThemedNotificationContent,
};

function getTone(palette: Palette, variant: VariantType): PaletteColor {
  switch (variant) {
    case "success":
      return palette.primary;
    case "info":
    case "default":
      return palette.secondary;
    case "warning":
      return palette.warning;
    case "error":
      return palette.error;
  }
}

function getIcon(variant: VariantType) {
  switch (variant) {
    case "success":
      return <CheckCircleRoundedIcon fontSize="small" />;
    case "error":
      return <ErrorOutlineRoundedIcon fontSize="small" />;
    case "warning":
      return <WarningAmberRoundedIcon fontSize="small" />;
    case "info":
    case "default":
      return <InfoOutlinedIcon fontSize="small" />;
  }
}
