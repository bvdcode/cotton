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
} from "@mui/material";
import { Shield, ShieldOutlined } from "@mui/icons-material";
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

  const submitLogin = useCallback(
    async () => {
      setError("");
      setLoading(true);

      try {
        if (requiresTwoFactor && normalizeTwoFactorCode(twoFactorCode).length < 6) {
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
    },
    [
      requiresTwoFactor,
      twoFactorCode,
      username,
      password,
      trustDevice,
      t,
      setAuthenticated,
      navigate,
    ],
  );

  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      await submitLogin();
    },
    [submitLogin],
  );

  const isEmail = (value: string): boolean =>
    /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());

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
            {!requiresTwoFactor && (
              <>
                <TextField
                  fullWidth
                  label={t("usernameLabel")}
                  margin="normal"
                  variant="outlined"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  disabled={loading}
                />
                <TextField
                  fullWidth
                  label={t("passwordLabel")}
                  type="password"
                  margin="normal"
                  variant="outlined"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={loading}
                />
              </>
            )}

            {requiresTwoFactor && (
              <Box sx={{ mt: 3 }}>
                <Typography
                  variant="body2"
                  color="text.secondary"
                  sx={{ mb: 3 }}
                  align="center"
                >
                  {t("twoFactor.caption")}
                </Typography>
                <Box sx={{ mb: 2 }}>
                  <OneTimeCodeInput
                    value={twoFactorCode}
                    onChange={setTwoFactorCode}
                    disabled={loading}
                    autoFocus={true}
                    inputAriaLabel={t("twoFactor.digit")}
                  />
                </Box>
              </Box>
            )}
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
                <Tooltip title={t("rememberMe")}>
                  <IconButton
                    color={trustDevice ? "primary" : "default"}
                    onClick={() => setTrustDevice(!trustDevice)}
                    disabled={loading}
                    sx={{
                      border: 1,
                      borderColor: trustDevice ? "primary.main" : "divider",
                    }}
                  >
                    {trustDevice ? <Shield /> : <ShieldOutlined />}
                  </IconButton>
                </Tooltip>
              )}
            </Box>
            {!requiresTwoFactor && (
              <Box sx={{ mt: 1.5, textAlign: "center" }}>
                <Link
                  component="button"
                  type="button"
                  variant="body2"
                  onClick={handleForgotPassword}
                  underline="hover"
                  color="text.secondary"
                  sx={{
                    pointerEvents:
                      loading || forgotPasswordSending ? "none" : "auto",
                    opacity: loading || forgotPasswordSending ? 0.5 : 1,
                  }}
                >
                  {forgotPasswordSending
                    ? t("forgotPassword.sending")
                    : t("forgotPassword.link")}
                </Link>
              </Box>
            )}
          </Box>
        </Paper>
      </Container>
    </>
  );
};
