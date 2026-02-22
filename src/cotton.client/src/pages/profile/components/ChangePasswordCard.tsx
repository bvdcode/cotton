import {
  Alert,
  Button,
  CircularProgress,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Tooltip,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { isAxiosError } from "../../../shared/api/httpClient";
import { authApi } from "../../../shared/api/authApi";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import PasswordIcon from "@mui/icons-material/Password";
import VisibilityIcon from "@mui/icons-material/Visibility";
import VisibilityOffIcon from "@mui/icons-material/VisibilityOff";

type ChangePasswordStatus =
  | { kind: "idle" }
  | { kind: "success" }
  | { kind: "error"; message: string };

type ApiErrorResponse = {
  message?: string;
  title?: string;
  errors?: Record<string, string | string[]>;
};

function extractApiErrorMessage(data?: ApiErrorResponse): string | null {
  if (!data) return null;
  if (typeof data.message === "string" && data.message.length > 0) {
    return data.message;
  }

  const errors = data.errors;
  if (errors && typeof errors === "object") {
    const values = Object.values(errors);
    for (const value of values) {
      if (typeof value === "string" && value.length > 0) {
        return value;
      }
      if (Array.isArray(value)) {
        const first = value.find((v) => typeof v === "string" && v.length > 0);
        if (first) {
          return first;
        }
      }
    }
  }

  if (typeof data.title === "string" && data.title.length > 0) {
    return data.title;
  }

  return null;
}

export const ChangePasswordCard = () => {
  const { t } = useTranslation("profile");

  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [showOldPassword, setShowOldPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmNewPassword, setShowConfirmNewPassword] = useState(false);
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
        const data = e.response?.data as ApiErrorResponse | undefined;
        const message = extractApiErrorMessage(data);
        if (message) {
          setStatus({ kind: "error", message: message });
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
      <Stack spacing={2} paddingY={2}>
        {status.kind === "success" && (
          <Alert severity="success">{t("password.success")}</Alert>
        )}
        {status.kind === "error" && (
          <Alert severity="error">{status.message}</Alert>
        )}

        <TextField
          label={t("password.old")}
          type={showOldPassword ? "text" : "password"}
          value={oldPassword}
          onChange={(e) => setOldPassword(e.target.value)}
          autoComplete="current-password"
          fullWidth
          slotProps={{
            input: {
              endAdornment: (
                <InputAdornment position="end">
                  <Tooltip
                    title={
                      showOldPassword ? t("password.hide") : t("password.show")
                    }
                  >
                    <IconButton
                      edge="end"
                      onClick={() => setShowOldPassword((v) => !v)}
                      aria-label={
                        showOldPassword
                          ? t("password.hide")
                          : t("password.show")
                      }
                    >
                      {showOldPassword ? (
                        <VisibilityIcon />
                      ) : (
                        <VisibilityOffIcon />
                      )}
                    </IconButton>
                  </Tooltip>
                </InputAdornment>
              ),
            },
          }}
        />

        <TextField
          label={t("password.new")}
          type={showNewPassword ? "text" : "password"}
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          autoComplete="new-password"
          fullWidth
          slotProps={{
            input: {
              endAdornment: (
                <InputAdornment position="end">
                  <Tooltip
                    title={
                      showNewPassword ? t("password.hide") : t("password.show")
                    }
                  >
                    <IconButton
                      edge="end"
                      onClick={() => setShowNewPassword((v) => !v)}
                      aria-label={
                        showNewPassword
                          ? t("password.hide")
                          : t("password.show")
                      }
                    >
                      {showNewPassword ? (
                        <VisibilityIcon />
                      ) : (
                        <VisibilityOffIcon />
                      )}
                    </IconButton>
                  </Tooltip>
                </InputAdornment>
              ),
            },
          }}
        />

        <TextField
          label={t("password.confirm")}
          type={showConfirmNewPassword ? "text" : "password"}
          value={confirmNewPassword}
          onChange={(e) => setConfirmNewPassword(e.target.value)}
          autoComplete="new-password"
          fullWidth
          slotProps={{
            input: {
              endAdornment: (
                <InputAdornment position="end">
                  <Tooltip
                    title={
                      showConfirmNewPassword
                        ? t("password.hide")
                        : t("password.show")
                    }
                  >
                    <IconButton
                      edge="end"
                      onClick={() => setShowConfirmNewPassword((v) => !v)}
                      aria-label={
                        showConfirmNewPassword
                          ? t("password.hide")
                          : t("password.show")
                      }
                    >
                      {showConfirmNewPassword ? (
                        <VisibilityIcon />
                      ) : (
                        <VisibilityOffIcon />
                      )}
                    </IconButton>
                  </Tooltip>
                </InputAdornment>
              ),
            },
          }}
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
