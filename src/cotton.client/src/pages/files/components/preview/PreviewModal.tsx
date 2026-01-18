import { Dialog, IconButton, Box, useMediaQuery } from "@mui/material";
import { Close } from "@mui/icons-material";
import type { ReactNode } from "react";
import { useTheme } from "@mui/material/styles";

interface PreviewModalProps {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
}

export const PreviewModal = ({
  open,
  onClose,
  children,
}: PreviewModalProps) => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="lg"
      fullWidth
      fullScreen={isMobile}
      PaperProps={{
        sx: {
          height: { xs: "100dvh", sm: "90dvh", md: "90vh" },
          maxHeight: { xs: "100dvh", sm: "90dvh", md: "90vh" },
          borderRadius: { xs: 0, sm: 2, md: 2 },
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
