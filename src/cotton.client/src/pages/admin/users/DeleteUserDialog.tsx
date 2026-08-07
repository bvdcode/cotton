import React, { useState } from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { AdminUserDto } from "../../../shared/api/adminApi";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import { useDeleteAdminUserMutation } from "../../../shared/api/queries/admin";

interface DeleteUserDialogProps {
  open: boolean;
  user: AdminUserDto | null;
  onClose: () => void;
}

export const DeleteUserDialog: React.FC<DeleteUserDialogProps> = ({
  open,
  user,
  onClose,
}) => {
  if (!open || !user) {
    return null;
  }

  return (
    <DeleteUserDialogContent key={user.id} user={user} onClose={onClose} />
  );
};

interface DeleteUserDialogContentProps {
  user: AdminUserDto;
  onClose: () => void;
}

const DeleteUserDialogContent: React.FC<DeleteUserDialogContentProps> = ({
  user,
  onClose,
}) => {
  const { t } = useTranslation(["admin", "common"]);
  const [confirmation, setConfirmation] = useState("");
  const [error, setError] = useState<string | null>(null);
  const deleteUserMutation = useDeleteAdminUserMutation();
  const deleting = deleteUserMutation.isPending;
  const canDelete = confirmation === user.username && !deleting;

  const handleDelete = async () => {
    setError(null);

    try {
      await deleteUserMutation.mutateAsync(user.id);
      onClose();
    } catch (caughtError) {
      const message = getApiErrorMessage(caughtError);
      if (message) {
        setError(message);
        return;
      }

      setError(t("users.errors.deleteFailed"));
    }
  };

  const handleClose = () => {
    if (!deleting) {
      onClose();
    }
  };

  return (
    <Dialog open onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t("users.delete.title")}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} pt={1}>
          {error && <Alert severity="error">{error}</Alert>}
          <Alert severity="warning">{t("users.delete.warning")}</Alert>
          <Typography>
            {t("users.delete.confirmation", { username: user.username })}
          </Typography>
          <TextField
            label={t("users.delete.confirmationLabel")}
            value={confirmation}
            onChange={(event) => setConfirmation(event.target.value)}
            autoComplete="off"
            autoFocus
            fullWidth
            disabled={deleting}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={deleting}>
          {t("actions.cancel", { ns: "common" })}
        </Button>
        <Button
          color="error"
          variant="contained"
          onClick={handleDelete}
          disabled={!canDelete}
        >
          {deleting ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <CircularProgress size={16} color="inherit" />
              <span>{t("users.delete.deleting")}</span>
            </Stack>
          ) : (
            t("users.delete.button")
          )}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
