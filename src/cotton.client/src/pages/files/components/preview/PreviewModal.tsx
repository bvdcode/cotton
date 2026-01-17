import { Dialog, IconButton, Box } from "@mui/material";
import { Close } from "@mui/icons-material";
import type { ReactNode } from "react";

interface PreviewModalProps {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
}

export const PreviewModal = ({ open, onClose, children }: PreviewModalProps) => {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="lg"
      fullWidth
      PaperProps={{
        sx: {
          height: "90vh",
          maxHeight: "90vh",
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
            top: 8,
            right: 8,
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
