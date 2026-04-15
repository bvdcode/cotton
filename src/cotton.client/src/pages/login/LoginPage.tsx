import {
  Box,
  Paper,
  Button,
  CircularProgress,
  TextField,
  Container,
  Typography,
  Avatar,
  Alert,
  AlertTitle,
  Stack,
  Snackbar,
  Grow,
  IconButton,
  Tooltip,
  Link,
  Divider,
} from "@mui/material";
import {
  GitHub,
  Shield,
  ShieldOutlined,
  KeyboardArrowRight,
} from "@mui/icons-material";
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
import { useServerInfoStore } from "../../shared/store/serverInfoStore";

type LoginErrorData = {
  message?: string;
  detail?: string;
};

type ToastSeverity = "error" | "success";

type ToastState = {
  key: number;
  open: boolean;
  severity: ToastSeverity;
  message: string;
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

type FirstRunAlertProps = {
  title: string;
  message: string;
};

const FirstRunAlert: React.FC<FirstRunAlertProps> = ({ title, message }) => {
  return (
    <Alert severity="info">
      <AlertTitle>{title}</AlertTitle>
      {message}
    </Alert>
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
    <Stack spacing={2}>
      <TextField
        fullWidth
        label={usernameLabel}
        margin="none"
        variant="outlined"
        value={username}
        onChange={(e) => onUsernameChange(e.target.value)}
        disabled={disabled}
      />
      <TextField
        fullWidth
        label={passwordLabel}
        type="password"
        margin="none"
        variant="outlined"
        value={password}
        onChange={(e) => onPasswordChange(e.target.value)}
        disabled={disabled}
      />
    </Stack>
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
    <Stack spacing={2.5}>
      <Typography variant="body2" color="text.secondary" align="center">
        {caption}
      </Typography>
      <OneTimeCodeInput
        value={value}
        onChange={onChange}
        disabled={disabled}
        autoFocus={true}
        inputAriaLabel={digitAriaLabel}
      />
    </Stack>
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
        <GitHub fontSize="small" sx={{ mr: 0.5 }} />
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
  const [toast, setToast] = useState<ToastState>({
    key: 0,
    open: false,
    severity: "error",
    message: "",
  });

  const serverInfo = useServerInfoStore((s) => s.data);
  const fetchServerInfo = useServerInfoStore((s) => s.fetchServerInfo);

  useEffect(() => {
    fetchServerInfo();
  }, [fetchServerInfo]);

  const showToast = useCallback((message: string, severity: ToastSeverity) => {
    setToast({
      key: Date.now(),
      open: true,
      severity,
      message,
    });
  }, []);

  const handleToastClose = useCallback(
    (_event?: React.SyntheticEvent | Event, reason?: string) => {
      if (reason === "clickaway") return;
      setToast((prev) => ({
        ...prev,
        open: false,
      }));
    },
    [],
  );

  const submitLogin = useCallback(async () => {
    setLoading(true);

    try {
      if (
        requiresTwoFactor &&
        normalizeTwoFactorCode(twoFactorCode).length < 6
      ) {
        showToast(t("twoFactor.required"), "error");
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
          return;
        }

        if (hint === "invalid") {
          setRequiresTwoFactor(true);
          showToast(t("twoFactor.invalid"), "error");
          return;
        }

        if (hint === "locked") {
          setRequiresTwoFactor(true);
          showToast(t("twoFactor.locked"), "error");
          return;
        }
      }

      showToast(t("errorMessage"), "error");
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
    showToast,
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
    const trimmed = username.trim();
    if (!trimmed || !isEmail(trimmed)) {
      showToast(t("forgotPassword.enterEmail"), "error");
      return;
    }

    setForgotPasswordSending(true);
    try {
      await authApi.forgotPassword(trimmed);
      showToast(t("forgotPassword.sent"), "success");
    } catch {
      showToast(t("forgotPassword.sent"), "success");
    } finally {
      setForgotPasswordSending(false);
    }
  }, [username, t, showToast]);

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

  const showFirstRunAlert =
    serverInfo !== null && serverInfo.canCreateInitialAdmin;

  const isFirstRunMode = showFirstRunAlert && !requiresTwoFactor;

  const submitButtonLabel = isFirstRunMode
    ? t("firstRun.continueButton")
    : t("loginButton");

  return (
    <>
      {(isInitializing || showRestoreOverlay) && (
        <Loader
          overlay={true}
          title={t("restoring.title")}
          caption={t("restoring.caption")}
        />
      )}
      <Box
        sx={{
          minHeight: "100dvh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          py: { xs: 2, sm: 4 },
        }}
      >
        <Container
          maxWidth="xs"
          sx={{
            display: "flex",
            flexDirection: "column",
            px: { xs: 2, sm: 3 },
          }}
        >
          <Paper
            sx={{
              p: { xs: 3, sm: 4 },
            }}
          >
            <Box
              display="flex"
              justifyContent="space-between"
              alignItems="center"
              sx={{ mb: 3 }}
            >
              <Typography variant="h4" component="h1">
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
              <Stack spacing={2.5}>
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
                {showFirstRunAlert && (
                  <FirstRunAlert
                    title={t("firstRun.title")}
                    message={t("firstRun.message")}
                  />
                )}
                <Box
                  sx={{
                    display: "flex",
                    gap: 1,
                    justifyContent: "center",
                    alignItems: "center",
                  }}
                >
                  {!requiresTwoFactor && (
                    <TrustDeviceToggle
                      active={trustDevice}
                      onToggle={() => setTrustDevice((v) => !v)}
                      disabled={loading}
                      tooltip={t("rememberMe")}
                    />
                  )}
                  <Button
                    type="submit"
                    variant="contained"
                    color="primary"
                    disabled={loading}
                    sx={{
                      minWidth: loading ? 44 : 0,
                      px: loading ? 0.75 : 2.25,
                      transition: (theme) =>
                        theme.transitions.create(["min-width", "padding"], {
                          duration: theme.transitions.duration.shorter,
                        }),
                    }}
                  >
                    <Box
                      sx={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 1,
                      }}
                    >
                      <Box
                        component="span"
                        sx={{
                          overflow: "hidden",
                          whiteSpace: "nowrap",
                          maxWidth: loading ? 0 : 220,
                          opacity: loading ? 0 : 1,
                          transition: (theme) =>
                            theme.transitions.create(["max-width", "opacity"], {
                              duration: theme.transitions.duration.shorter,
                            }),
                        }}
                      >
                        {submitButtonLabel}
                      </Box>
                      <Box
                        sx={{
                          display: "inline-flex",
                          alignItems: "center",
                          justifyContent: "center",
                          width: 20,
                          height: 20,
                        }}
                      >
                        {loading ? (
                          <CircularProgress
                            color="inherit"
                            size={18}
                            thickness={5}
                          />
                        ) : (
                          <KeyboardArrowRight />
                        )}
                      </Box>
                    </Box>
                  </Button>
                </Box>
              </Stack>
            </Box>
          </Paper>
          {!requiresTwoFactor && !showFirstRunAlert && (
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
      </Box>
      <Snackbar
        key={toast.key}
        open={toast.open}
        onClose={handleToastClose}
        autoHideDuration={5000}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
        TransitionComponent={Grow}
        transitionDuration={{ enter: 260, exit: 220 }}
      >
        <Alert
          severity={toast.severity}
          variant="filled"
          onClose={handleToastClose}
        >
          {toast.message}
        </Alert>
      </Snackbar>
    </>
  );
};
