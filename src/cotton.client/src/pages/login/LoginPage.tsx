import {
  Box,
  Paper,
  Button,
  TextField,
  Container,
  Typography,
  Avatar,
  Alert,
  IconButton,
  Tooltip,
  Link,
  Divider,
} from "@mui/material";
import { GitHub, Shield, ShieldOutlined } from "@mui/icons-material";
import { useAuth } from "../../features/auth";
import { useTranslation } from "react-i18next";
import {
  useEffect,
  useState,
  useRef,
  useCallback,
  type FormEvent,
} from "react";
import { authApi } from "../../shared/api/authApi";
import { useNavigate, useLocation, Navigate } from "react-router-dom";
import Loader from "../../shared/ui/Loader";
import axios from "axios";
import { OneTimeCodeInput } from "../../shared/ui/OneTimeCodeInput";

type LoginErrorData = {
  message?: string;
  detail?: string;
};

type TwoFactorServerHint = "required" | "invalid" | "locked";

function normalizeTwoFactorCode(value: string): string {
  return value.replace(/\D/g, "").slice(0, 6);
}

function isEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}

function tryGetTwoFactorHint(args: {
  status: number | undefined;
  serverMessage: string | undefined;
}): TwoFactorServerHint | null {
  const { status, serverMessage } = args;
  if (status !== 403) return null;
  if (typeof serverMessage !== "string") return null;

  const msgLower = serverMessage.toLowerCase();

  if (msgLower.includes("two-factor") && msgLower.includes("required")) {
    return "required";
  }

  if (msgLower.includes("invalid") && msgLower.includes("two-factor")) {
    return "invalid";
  }

  if (
    msgLower.includes("maximum") ||
    msgLower.includes("locked") ||
    msgLower.includes("attempts")
  ) {
    return "locked";
  }

  return null;
}

type LoginAlertsProps = {
  error: string;
  forgotPasswordMessage: string;
};

const LoginAlerts: React.FC<LoginAlertsProps> = ({
  error,
  forgotPasswordMessage,
}) => {
  if (!error && !forgotPasswordMessage) return null;

  return (
    <>
      {error && (
        <Alert color="error" sx={{ mt: 2 }}>
          {error}
        </Alert>
      )}
      {forgotPasswordMessage && (
        <Alert color="success" sx={{ mt: 2 }}>
          {forgotPasswordMessage}
        </Alert>
      )}
    </>
  );
};

type CredentialsFieldsProps = {
  username: string;
  password: string;
  onUsernameChange: (value: string) => void;
  onPasswordChange: (value: string) => void;
  disabled: boolean;
  usernameLabel: string;
  passwordLabel: string;
};

const CredentialsFields: React.FC<CredentialsFieldsProps> = ({
  username,
  password,
  onUsernameChange,
  onPasswordChange,
  disabled,
  usernameLabel,
  passwordLabel,
}) => {
  return (
    <>
      <TextField
        fullWidth
        label={usernameLabel}
        margin="normal"
        variant="outlined"
        value={username}
        onChange={(e) => onUsernameChange(e.target.value)}
        disabled={disabled}
      />
      <TextField
        fullWidth
        label={passwordLabel}
        type="password"
        margin="normal"
        variant="outlined"
        value={password}
        onChange={(e) => onPasswordChange(e.target.value)}
        disabled={disabled}
      />
    </>
  );
};

type TwoFactorFieldsProps = {
  caption: string;
  digitAriaLabel: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
};

const TwoFactorFields: React.FC<TwoFactorFieldsProps> = ({
  caption,
  digitAriaLabel,
  value,
  onChange,
  disabled,
}) => {
  return (
    <Box sx={{ mt: 3 }}>
      <Typography
        variant="body2"
        color="text.secondary"
        sx={{ mb: 3 }}
        align="center"
      >
        {caption}
      </Typography>
      <Box sx={{ mb: 2 }}>
        <OneTimeCodeInput
          value={value}
          onChange={onChange}
          disabled={disabled}
          autoFocus={true}
          inputAriaLabel={digitAriaLabel}
        />
      </Box>
    </Box>
  );
};

type TrustDeviceToggleProps = {
  active: boolean;
  onToggle: () => void;
  disabled: boolean;
  tooltip: string;
};

const TrustDeviceToggle: React.FC<TrustDeviceToggleProps> = ({
  active,
  onToggle,
  disabled,
  tooltip,
}) => {
  return (
    <Tooltip title={tooltip}>
      <IconButton
        color={active ? "primary" : "default"}
        onClick={onToggle}
        disabled={disabled}
        sx={{
          border: 1,
          borderColor: active ? "primary.main" : "divider",
        }}
      >
        {active ? <Shield /> : <ShieldOutlined />}
      </IconButton>
    </Tooltip>
  );
};

type ForgotPasswordLinkProps = {
  onClick: () => void;
  disabled: boolean;
  label: string;
};

const ForgotPasswordLink: React.FC<ForgotPasswordLinkProps> = ({
  onClick,
  disabled,
  label,
}) => {
  return (
    <Box
      display="flex"
      justifyContent="center"
      alignItems="center"
      sx={{ mt: 1.5, textAlign: "center" }}
    >
      <Link
        href="https://github.com/bvdcode/cotton"
        target="_blank"
        rel="noopener"
        underline="hover"
        color="text.secondary"
        sx={{ display: "flex", alignItems: "center" }}
      >
        <GitHub fontSize="small" sx={{ verticalAlign: "middle", mr: 0.5 }} />
      </Link>
      <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />
      <Link
        component="button"
        type="button"
        variant="caption"
        onClick={onClick}
        underline="hover"
        color="text.secondary"
        sx={{
          pointerEvents: disabled ? "none" : "auto",
          opacity: disabled ? 0.5 : 1,
        }}
      >
        {label}
      </Link>
    </Box>
  );
};

export const LoginPage = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { t } = useTranslation("login");
  const [error, setError] = useState("");
  const {
    isAuthenticated,
    isInitializing,
    hydrated,
    refreshEnabled,
    hasChecked,
    ensureAuth,
    setAuthenticated,
  } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [trustDevice, setTrustDevice] = useState(false);
  const [requiresTwoFactor, setRequiresTwoFactor] = useState(false);
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [loading, setLoading] = useState(false);
  const autoSubmitTriggeredRef = useRef(false);
  const [forgotPasswordSending, setForgotPasswordSending] = useState(false);
  const [forgotPasswordMessage, setForgotPasswordMessage] = useState("");

  const submitLogin = useCallback(async () => {
    setError("");
    setLoading(true);

    try {
      if (
        requiresTwoFactor &&
        normalizeTwoFactorCode(twoFactorCode).length < 6
      ) {
        setError(t("twoFactor.required"));
        return;
      }

      await authApi.login({
        username,
        password,
        twoFactorCode: requiresTwoFactor
          ? normalizeTwoFactorCode(twoFactorCode)
          : undefined,
        trustDevice,
      });

      const user = await authApi.me();
      setAuthenticated(true, user);
      navigate("/");
    } catch (e) {
      if (axios.isAxiosError(e)) {
        const status = e.response?.status;
        const data = e.response?.data as LoginErrorData | undefined;
        const serverMessage = data?.detail ?? data?.message;
        const hint = tryGetTwoFactorHint({ status, serverMessage });

        if (hint === "required") {
          setRequiresTwoFactor(true);
          setTwoFactorCode("");
          setError("");
          return;
        }

        if (hint === "invalid") {
          setRequiresTwoFactor(true);
          setError(t("twoFactor.invalid"));
          return;
        }

        if (hint === "locked") {
          setRequiresTwoFactor(true);
          setError(t("twoFactor.locked"));
          return;
        }
      }

      setError(t("errorMessage"));
    } finally {
      setLoading(false);
    }
  }, [
    requiresTwoFactor,
    twoFactorCode,
    username,
    password,
    trustDevice,
    t,
    setAuthenticated,
    navigate,
  ]);

  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      await submitLogin();
    },
    [submitLogin],
  );

  const handleForgotPassword = useCallback(async () => {
    setError("");
    setForgotPasswordMessage("");

    const trimmed = username.trim();
    if (!trimmed || !isEmail(trimmed)) {
      setError(t("forgotPassword.enterEmail"));
      return;
    }

    setForgotPasswordSending(true);
    try {
      await authApi.forgotPassword(trimmed);
      setForgotPasswordMessage(t("forgotPassword.sent"));
    } catch {
      setForgotPasswordMessage(t("forgotPassword.sent"));
    } finally {
      setForgotPasswordSending(false);
    }
  }, [username, t]);

  useEffect(() => {
    // If we can silently restore a session, do it and keep loader overlay while it runs.
    if (!hydrated) return;
    if (!refreshEnabled) return;
    if (isAuthenticated) return;
    if (hasChecked) return;
    ensureAuth();
  }, [hydrated, refreshEnabled, isAuthenticated, hasChecked, ensureAuth]);

  // Auto-submit when 2FA code is complete (6 digits)
  useEffect(() => {
    if (!requiresTwoFactor) {
      autoSubmitTriggeredRef.current = false;
      return;
    }

    const cleanCode = normalizeTwoFactorCode(twoFactorCode);
    if (cleanCode.length === 6 && !loading && !autoSubmitTriggeredRef.current) {
      autoSubmitTriggeredRef.current = true;

      // Small delay to ensure state is stable
      const timer = setTimeout(() => {
        void submitLogin();
      }, 100);

      return () => clearTimeout(timer);
    }

    // Reset flag when code changes (user editing)
    if (cleanCode.length < 6) {
      autoSubmitTriggeredRef.current = false;
    }
  }, [twoFactorCode, requiresTwoFactor, loading, submitLogin]);

  // Redirect to home if already authenticated
  if (!isInitializing && isAuthenticated) {
    const from = (location.state as { from?: string })?.from || "/";
    return <Navigate to={from} replace />;
  }

  const showRestoreOverlay =
    hydrated && refreshEnabled && !isAuthenticated && !hasChecked;

  return (
    <>
      {(isInitializing || showRestoreOverlay) && (
        <Loader
          overlay={true}
          title={t("restoring.title")}
          caption={t("restoring.caption")}
        />
      )}
      <Container maxWidth="sm">
        <Paper
          sx={{
            mt: 8,
            p: 4,
          }}
        >
          <Box
            display="flex"
            justifyContent="space-between"
            alignItems="center"
          >
            <Typography variant="h4" component="h1" gutterBottom>
              {requiresTwoFactor ? t("twoFactor.title") : t("title")}
            </Typography>
            <Avatar src="/assets/icons/icon.svg" alt="App Logo" />
          </Box>
          <Box
            component="form"
            onSubmit={handleSubmit}
            noValidate
            autoComplete="off"
          >
            {!requiresTwoFactor ? (
              <CredentialsFields
                username={username}
                password={password}
                onUsernameChange={setUsername}
                onPasswordChange={setPassword}
                disabled={loading}
                usernameLabel={t("usernameLabel")}
                passwordLabel={t("passwordLabel")}
              />
            ) : (
              <TwoFactorFields
                caption={t("twoFactor.caption")}
                digitAriaLabel={t("twoFactor.digit")}
                value={twoFactorCode}
                onChange={setTwoFactorCode}
                disabled={loading}
              />
            )}

            <LoginAlerts
              error={error}
              forgotPasswordMessage={forgotPasswordMessage}
            />
            <Box sx={{ mt: 3, display: "flex", gap: 1 }}>
              <Button
                type="submit"
                variant="contained"
                color="primary"
                disabled={loading}
                fullWidth
              >
                {loading ? t("loggingIn") : t("loginButton")}
              </Button>
              {!requiresTwoFactor && (
                <TrustDeviceToggle
                  active={trustDevice}
                  onToggle={() => setTrustDevice((v) => !v)}
                  disabled={loading}
                  tooltip={t("rememberMe")}
                />
              )}
            </Box>
          </Box>
        </Paper>
        {!requiresTwoFactor && (
          <ForgotPasswordLink
            onClick={handleForgotPassword}
            disabled={loading || forgotPasswordSending}
            label={
              forgotPasswordSending
                ? t("forgotPassword.sending")
                : t("forgotPassword.link")
            }
          />
        )}
      </Container>
    </>
  );
};
