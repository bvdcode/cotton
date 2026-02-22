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

type PasswordInputFieldProps = {
  label: string;
  value: string;
  onChange: (value: string) => void;
  autoComplete: string;
  visible: boolean;
  onToggleVisibility: () => void;
  showLabel: string;
  hideLabel: string;
  disabled: boolean;
};

const PasswordInputField: React.FC<PasswordInputFieldProps> = ({
  label,
  value,
  onChange,
  autoComplete,
  visible,
  onToggleVisibility,
  showLabel,
  hideLabel,
  disabled,
}) => {
  const tooltipTitle = visible ? hideLabel : showLabel;

  return (
    <TextField
      label={label}
      type={visible ? "text" : "password"}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      autoComplete={autoComplete}
      fullWidth
      disabled={disabled}
      slotProps={{
        input: {
          endAdornment: (
            <InputAdornment position="end">
              <Tooltip title={tooltipTitle}>
                <IconButton
                  edge="end"
                  onClick={onToggleVisibility}
                  aria-label={tooltipTitle}
                  disabled={disabled}
                >
                  {visible ? <VisibilityIcon /> : <VisibilityOffIcon />}
                </IconButton>
              </Tooltip>
            </InputAdornment>
          ),
        },
      }}
    />
  );
};

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
        <PasswordInputField
          label={t("password.old")}
          value={oldPassword}
          onChange={setOldPassword}
          autoComplete="current-password"
          visible={showOldPassword}
          onToggleVisibility={() => setShowOldPassword((v) => !v)}
          showLabel={t("password.show")}
          hideLabel={t("password.hide")}
          disabled={loading}
        />

        <PasswordInputField
          label={t("password.new")}
          value={newPassword}
          onChange={setNewPassword}
          autoComplete="new-password"
          visible={showNewPassword}
          onToggleVisibility={() => setShowNewPassword((v) => !v)}
          showLabel={t("password.show")}
          hideLabel={t("password.hide")}
          disabled={loading}
        />

        <PasswordInputField
          label={t("password.confirm")}
          value={confirmNewPassword}
          onChange={setConfirmNewPassword}
          autoComplete="new-password"
          visible={showConfirmNewPassword}
          onToggleVisibility={() => setShowConfirmNewPassword((v) => !v)}
          showLabel={t("password.show")}
          hideLabel={t("password.hide")}
          disabled={loading}
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
