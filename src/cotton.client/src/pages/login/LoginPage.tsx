import {
  Avatar,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Container,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { KeyboardArrowRight, KeyOutlined } from "@mui/icons-material";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../../features/auth";
import Loader from "../../shared/ui/Loader";
import { useServerInfoStore } from "../../shared/store/serverInfoStore";
import { CredentialsFields } from "./components/CredentialsFields";
import { FirstRunAlert } from "./components/FirstRunAlert";
import { ForgotPasswordLink } from "./components/ForgotPasswordLink";
import { TrustDeviceToggle } from "./components/TrustDeviceToggle";
import { TwoFactorFields } from "./components/TwoFactorFields";
import { useLoginForm } from "./useLoginForm";

export const LoginPage = () => {
  const location = useLocation();
  const { t } = useTranslation("login");
  const {
    isAuthenticated,
    isInitializing,
    hydrated,
    refreshEnabled,
    hasChecked,
    ensureAuth,
  } = useAuth();
  const form = useLoginForm();

  const serverInfo = useServerInfoStore((s) => s.data);
  const fetchServerInfo = useServerInfoStore((s) => s.fetchServerInfo);

  useEffect(() => {
    fetchServerInfo();
  }, [fetchServerInfo]);

  useEffect(() => {
    if (!hydrated) return;
    if (!refreshEnabled) return;
    if (isAuthenticated) return;
    if (hasChecked) return;
    ensureAuth();
  }, [hydrated, refreshEnabled, isAuthenticated, hasChecked, ensureAuth]);

  if (!isInitializing && isAuthenticated) {
    const from = (location.state as { from?: string })?.from || "/";
    return <Navigate to={from} replace />;
  }

  const showRestoreOverlay =
    hydrated && refreshEnabled && !isAuthenticated && !hasChecked;

  const showFirstRunAlert =
    serverInfo !== null && serverInfo.canCreateInitialAdmin;

  const isFirstRunMode = showFirstRunAlert && !form.requiresTwoFactor;

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
                {form.requiresTwoFactor ? t("twoFactor.title") : t("title")}
              </Typography>
              <Avatar src="/assets/icons/icon.svg" alt="App Logo" />
            </Box>
            <Box
              component="form"
              onSubmit={form.handleSubmit}
              noValidate
              autoComplete="off"
            >
              <Stack spacing={2.5}>
                {!form.requiresTwoFactor ? (
                  <CredentialsFields
                    username={form.username}
                    password={form.password}
                    onUsernameChange={form.setUsername}
                    onUsernameBlur={form.markUsernameBlurred}
                    onPasswordChange={form.setPassword}
                    disabled={form.loading}
                    usernameLabel={t("usernameLabel")}
                    passwordLabel={t("passwordLabel")}
                    usernameHasError={form.usernameHasError}
                  />
                ) : (
                  <TwoFactorFields
                    caption={t("twoFactor.caption")}
                    digitAriaLabel={t("twoFactor.digit")}
                    value={form.twoFactorCode}
                    onChange={form.setTwoFactorCode}
                    disabled={form.loading}
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
                  {!form.requiresTwoFactor && (
                    <TrustDeviceToggle
                      active={form.trustDevice}
                      onToggle={form.toggleTrustDevice}
                      disabled={form.loading}
                      tooltip={t("rememberMe")}
                    />
                  )}
                  {!form.requiresTwoFactor && (
                    <Tooltip title={t("passkey.loginButton")}>
                      <span>
                        <IconButton
                          type="button"
                          color="primary"
                          onClick={form.handlePasskeyLogin}
                          disabled={form.loading || form.passkeyLoading}
                          aria-label={t("passkey.loginButton")}
                          sx={{
                            border: "1px solid",
                            borderColor: "divider",
                            width: 40,
                            height: 40,
                          }}
                        >
                          {form.passkeyLoading ? (
                            <CircularProgress color="inherit" size={18} />
                          ) : (
                            <KeyOutlined />
                          )}
                        </IconButton>
                      </span>
                    </Tooltip>
                  )}
                  <Button
                    type="submit"
                    variant="contained"
                    color="primary"
                    disabled={form.loading}
                    sx={{
                      minWidth: form.loading ? 44 : 0,
                      px: form.loading ? 0.75 : 2.25,
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
                          maxWidth: form.loading ? 0 : 220,
                          opacity: form.loading ? 0 : 1,
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
                        {form.loading ? (
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
          {!form.requiresTwoFactor && !showFirstRunAlert && (
            <ForgotPasswordLink
              onClick={form.handleForgotPassword}
              disabled={form.loading || form.forgotPasswordSending}
              label={
                form.forgotPasswordSending
                  ? t("forgotPassword.sending")
                  : t("forgotPassword.link")
              }
            />
          )}
        </Container>
      </Box>
    </>
  );
};
