import { Dialog, IconButton, Box, Typography, useMediaQuery } from "@mui/material";
import { Close } from "@mui/icons-material";
import type { ReactNode } from "react";
import { useTheme } from "@mui/material/styles";

interface PreviewModalProps {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  title?: ReactNode;
  layout?: "overlay" | "header";
  forceFullScreen?: boolean;
  headerActions?: ReactNode;
}

export const PreviewModal = ({
  open,
  onClose,
  children,
  title,
  layout = "overlay",
  forceFullScreen = false,
  headerActions,
}: PreviewModalProps) => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const isFullScreen = isMobile || forceFullScreen;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth={forceFullScreen ? false : "lg"}
      fullWidth
      fullScreen={isFullScreen}
      PaperProps={{
        sx: {
          height: isFullScreen
            ? "100dvh"
            : { xs: "100dvh", sm: "90dvh", md: "90vh" },
          maxHeight: isFullScreen
            ? "100dvh"
            : { xs: "100dvh", sm: "90dvh", md: "90vh" },
          width: forceFullScreen ? "100vw" : undefined,
          maxWidth: forceFullScreen ? "100vw" : undefined,
          borderRadius: isFullScreen ? 0 : { xs: 0, sm: 2, md: 2 },
        },
      }}
    >
      <Box
        sx={{
          position: "relative",
          height: "100%",
          display: "flex",
          flexDirection: "column",
        }}
      >
        {layout === "overlay" && (
          <Box
            sx={{
              position: "absolute",
              top: "calc(env(safe-area-inset-top, 0px) + 8px)",
              right: "calc(env(safe-area-inset-right, 0px) + 8px)",
              zIndex: 1,
            }}
          >
            <IconButton
              onClick={onClose}
              sx={{
                bgcolor: "background.paper",
                "&:hover": {
                  bgcolor: "action.hover",
                },
              }}
            >
              <Close />
            </IconButton>
          </Box>
        )}

        {layout === "header" && (
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 1,
              px: 2,
              pt: "calc(env(safe-area-inset-top, 0px) + 8px)",
              pb: 1,
              borderBottom: 1,
              borderColor: "divider",
            }}
          >
            <Typography variant="subtitle2" sx={{ flex: 1, minWidth: 0 }} noWrap>
              {title}
            </Typography>
            {headerActions}
            <IconButton
              onClick={onClose}
              sx={{
                bgcolor: "background.paper",
                "&:hover": {
                  bgcolor: "action.hover",
                },
              }}
            >
              <Close />
            </IconButton>
          </Box>
        )}
        <Box
          sx={{
            flex: 1,
            overflow: "hidden",
            display: "flex",
            flexDirection: "column",
          }}
        >
          {children}
        </Box>
      </Box>
    </Dialog>
  );
};
