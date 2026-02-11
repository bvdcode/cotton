import {
  Box,
  Paper,
  Button,
  TextField,
  Container,
  Typography,
  Avatar,
  Alert,
  Checkbox,
  FormControlLabel,
} from "@mui/material";
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

  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      setError("");
      setLoading(true);

      try {
        if (requiresTwoFactor && twoFactorCode.replace(/\D/g, "").length < 6) {
          setError(t("twoFactor.required"));
          return;
        }

        await authApi.login({
          username,
          password,
          twoFactorCode: requiresTwoFactor
            ? twoFactorCode.replace(/\D/g, "").slice(0, 6)
            : undefined,
          trustDevice,
        });
        const user = await authApi.me();
        setAuthenticated(true, user);
        navigate("/");
      } catch (e) {
        if (axios.isAxiosError(e)) {
          const status = e.response?.status;
          const data = e.response?.data as {
            message?: string;
            detail?: string;
          };
          const serverMessage = data?.detail ?? data?.message;

          if (status === 403 && typeof serverMessage === "string") {
            const msgLower = serverMessage.toLowerCase();

            // Check if 2FA code is required
            if (
              msgLower.includes("two-factor") &&
              msgLower.includes("required")
            ) {
              setRequiresTwoFactor(true);
              setTwoFactorCode("");
              setError("");
              return;
            }

            // Check if 2FA code is invalid
            if (
              msgLower.includes("invalid") &&
              msgLower.includes("two-factor")
            ) {
              setRequiresTwoFactor(true);
              setError(t("twoFactor.invalid"));
              return;
            }

            // Check if locked due to too many attempts
            if (
              msgLower.includes("maximum") ||
              msgLower.includes("locked") ||
              msgLower.includes("attempts")
            ) {
              setRequiresTwoFactor(true);
              setError(t("twoFactor.locked"));
              return;
            }
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

    const cleanCode = twoFactorCode.replace(/\D/g, "");
    if (cleanCode.length === 6 && !loading && !autoSubmitTriggeredRef.current) {
      autoSubmitTriggeredRef.current = true;

      // Small delay to ensure state is stable
      const timer = setTimeout(() => {
        const submitEvent = new Event("submit", {
          bubbles: true,
          cancelable: true,
        });
        Object.defineProperty(submitEvent, "preventDefault", {
          value: () => {},
          writable: false,
        });
        handleSubmit(submitEvent as unknown as FormEvent);
      }, 100);

      return () => clearTimeout(timer);
    }

    // Reset flag when code changes (user editing)
    if (cleanCode.length < 6) {
      autoSubmitTriggeredRef.current = false;
    }
  }, [twoFactorCode, requiresTwoFactor, loading, handleSubmit]);

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
          title="Restoring session..."
          caption="Please, wait"
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

                <FormControlLabel
                  sx={{ mt: 1, userSelect: "none" }}
                  control={
                    <Checkbox
                      checked={trustDevice}
                      onChange={(e) => setTrustDevice(e.target.checked)}
                      disabled={loading}
                    />
                  }
                  label={t("rememberMe")}
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
            <Button
              type="submit"
              variant="contained"
              color="primary"
              disabled={loading}
              fullWidth
              sx={{ mt: 3 }}
            >
              {loading ? t("loggingIn") : t("loginButton")}
            </Button>
          </Box>
        </Paper>
      </Container>
    </>
  );
};
