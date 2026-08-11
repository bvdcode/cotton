import React from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemText,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";

interface SkippedUploadItemsDialogProps {
  items: string[];
  onClose: () => void;
  open: boolean;
  total: number;
  truncated: boolean;
}

export const SkippedUploadItemsDialog: React.FC<
  SkippedUploadItemsDialogProps
> = ({ items, onClose, open, total, truncated }) => {
  const { t } = useTranslation(["files", "common"]);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>
        {t("uploadDrop.skippedDialog.title", { ns: "files" })}
      </DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {t("uploadDrop.skippedDialog.description", {
            ns: "files",
            count: total,
          })}
        </Typography>

        {items.length > 0 && (
          <List dense disablePadding sx={{ maxHeight: 360, overflow: "auto" }}>
            {items.map((item, index) => (
              <ListItem key={`${item}-${index}`} disableGutters>
                <ListItemText
                  primary={item}
                  primaryTypographyProps={{
                    variant: "body2",
                    sx: { overflowWrap: "anywhere", wordBreak: "break-word" },
                  }}
                />
              </ListItem>
            ))}
          </List>
        )}

        {truncated && (
          <Alert severity="info" sx={{ mt: 2 }}>
            {t("uploadDrop.skippedDialog.truncated", {
              ns: "files",
              count: items.length,
            })}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t("common:actions.close")}</Button>
      </DialogActions>
    </Dialog>
  );
};
