import { Alert, Button, CircularProgress, Stack, TextField } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { isAxiosError } from "../../../shared/api/httpClient";
import { authApi } from "../../../shared/api/authApi";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import PasswordIcon from "@mui/icons-material/Password";

type ChangePasswordStatus =
  | { kind: "idle" }
  | { kind: "success" }
  | { kind: "error"; message: string };

export const ChangePasswordCard = () => {
  const { t } = useTranslation("profile");

  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState<ChangePasswordStatus>({ kind: "idle" });

  const canSubmit =
    oldPassword.length > 0 &&
    newPassword.length > 0 &&
    confirmNewPassword.length > 0 &&
    !loading;

  const resetForm = () => {
    setOldPassword("");
    setNewPassword("");
    setConfirmNewPassword("");
  };

  const handleSubmit = async () => {
    setStatus({ kind: "idle" });

    if (newPassword !== confirmNewPassword) {
      setStatus({ kind: "error", message: t("password.errors.mismatch") });
      return;
    }

    setLoading(true);
    try {
      await authApi.changePassword({ oldPassword, newPassword });
      setStatus({ kind: "success" });
      resetForm();
    } catch (e) {
      if (isAxiosError(e)) {
        const message = (e.response?.data as { message?: string } | undefined)
          ?.message;
        if (typeof message === "string" && message.length > 0) {
          setStatus({ kind: "error", message });
          return;
        }
      }
      setStatus({ kind: "error", message: t("password.errors.failed") });
    } finally {
      setLoading(false);
    }
  };

  return (
    <ProfileAccordionCard
      id="password-header"
      ariaControls="password-content"
      icon={<PasswordIcon color="primary" />}
      title={t("password.title")}
      description={t("password.description")}
    >
      <Stack spacing={2}>
        {status.kind === "success" && (
          <Alert severity="success">{t("password.success")}</Alert>
        )}
        {status.kind === "error" && (
          <Alert severity="error">{status.message}</Alert>
        )}

        <TextField
          label={t("password.old")}
          type="password"
          value={oldPassword}
          onChange={(e) => setOldPassword(e.target.value)}
          autoComplete="current-password"
          fullWidth
        />

        <TextField
          label={t("password.new")}
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          autoComplete="new-password"
          fullWidth
        />

        <TextField
          label={t("password.confirm")}
          type="password"
          value={confirmNewPassword}
          onChange={(e) => setConfirmNewPassword(e.target.value)}
          autoComplete="new-password"
          fullWidth
        />

        <Stack direction="row" justifyContent="flex-end" spacing={1}>
          <Button
            variant="contained"
            onClick={handleSubmit}
            disabled={!canSubmit}
          >
            {loading ? (
              <Stack direction="row" spacing={1} alignItems="center">
                <CircularProgress size={16} />
                <span>{t("password.saving")}</span>
              </Stack>
            ) : (
              t("password.save")
            )}
          </Button>
        </Stack>
      </Stack>
    </ProfileAccordionCard>
  );
};
