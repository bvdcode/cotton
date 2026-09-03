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
import { clearOidcSignInPending } from "../../features/auth/oidcSignInSession";
import { useServerInfoStore } from "../../shared/store/serverInfoStore";
import { getSafeAuthReturnPath } from "../../shared/utils/authReturnPath";
import { CredentialsFields } from "./components/CredentialsFields";
import { LoginInfoAlert } from "./components/LoginInfoAlert";
import { ForgotPasswordLink } from "./components/ForgotPasswordLink";
import { TrustDeviceToggle } from "./components/TrustDeviceToggle";
import { TwoFactorFields } from "./components/TwoFactorFields";
import { OidcProviderButtons } from "./components/OidcProviderButtons";
import { useLoginForm } from "./useLoginForm";
import { readStringProperty } from "../../shared/utils/typeGuards";

type LoginFormState = ReturnType<typeof useLoginForm>;

export const LoginPage = () => {
  const location = useLocation();
  const returnUrl = getSafeAuthReturnPath(
    readStringProperty(location.state, "from") ?? "/",
  );
  const auth = useAuth();
  const form = useLoginForm({ disabled: auth.phase === "booting" });
  const serverInfo = useServerInfoStore((s) => s.data);
  const fetchServerInfo = useServerInfoStore((s) => s.fetchServerInfo);

  useEffect(() => {
    fetchServerInfo();
  }, [fetchServerInfo]);

  useEffect(() => {
    clearOidcSignInPending();
  }, []);

  if (auth.phase === "authenticated") {
    return <Navigate to={returnUrl} replace />;
  }

  const showFirstRunAlert =
    serverInfo !== null && serverInfo.canCreateInitialAdmin;
  const showDemoAlert =
    serverInfo?.isPublicInstance === true && !form.requiresTwoFactor;
  const isFirstRunMode = showFirstRunAlert && !form.requiresTwoFactor;

  return (
    <LoginShell
      footer={
        <LoginForgotPasswordLink
          form={form}
          show={!form.requiresTwoFactor && !showFirstRunAlert}
        />
      }
    >
      <LoginHeader form={form} />
      <Box
        component="form"
        onSubmit={form.handleSubmit}
        noValidate
        autoComplete="off"
      >
        <Stack spacing={2.5}>
          <LoginFormFields form={form} />
          {showDemoAlert && <DemoInstanceNotice />}
          {showFirstRunAlert && <FirstRunNotice />}
          <LoginActions form={form} isFirstRunMode={isFirstRunMode} />
          <OidcProviderButtons
            disabled={form.loading || form.disabled}
            returnUrl={returnUrl}
            trustDevice={form.trustDevice}
            visible={!form.requiresTwoFactor && !showFirstRunAlert}
          />
        </Stack>
      </Box>
    </LoginShell>
  );
};

type LoginShellProps = {
  children: React.ReactNode;
  footer?: React.ReactNode;
};

const LoginShell = ({ children, footer }: LoginShellProps) => (
  <Box
    sx={{
      minHeight: "100%",
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
      <Paper sx={{ p: { xs: 3, sm: 4 } }}>{children}</Paper>
      {footer}
    </Container>
  </Box>
);

type LoginHeaderProps = {
  form: LoginFormState;
};

const LoginHeader = ({ form }: LoginHeaderProps) => {
  const { t } = useTranslation("login");

  return (
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
  );
};

type LoginFormFieldsProps = {
  form: LoginFormState;
};

const LoginFormFields = ({ form }: LoginFormFieldsProps) => {
  const { t } = useTranslation("login");

  return form.requiresTwoFactor ? (
    <TwoFactorFields
      caption={t("twoFactor.caption")}
      digitAriaLabel={t("twoFactor.digit")}
      value={form.twoFactorCode}
      onChange={form.setTwoFactorCode}
      disabled={form.loading || form.disabled}
    />
  ) : (
    <CredentialsFields
      username={form.username}
      password={form.password}
      onUsernameChange={form.setUsername}
      onUsernameBlur={form.markUsernameBlurred}
      onPasswordChange={form.setPassword}
      disabled={form.loading || form.disabled}
      usernameLabel={t("usernameLabel")}
      passwordLabel={t("passwordLabel")}
      usernameHasError={form.usernameHasError}
    />
  );
};

const FirstRunNotice = () => {
  const { t } = useTranslation("login");

  return (
    <LoginInfoAlert
      title={t("firstRun.title")}
      message={t("firstRun.message")}
    />
  );
};

const DemoInstanceNotice = () => {
  const { t } = useTranslation("login");

  return (
    <LoginInfoAlert
      title={t("demoInstance.title")}
      message={t("demoInstance.message")}
    />
  );
};

type LoginActionsProps = {
  form: LoginFormState;
  isFirstRunMode: boolean;
};

const LoginActions = ({ form, isFirstRunMode }: LoginActionsProps) => (
  <Box
    sx={{
      display: "flex",
      gap: 1.5,
      justifyContent: "center",
      alignItems: "center",
    }}
  >
    {!form.requiresTwoFactor && <TrustDeviceButton form={form} />}
    {!form.requiresTwoFactor && <PasskeyLoginButton form={form} />}
    <LoginSubmitButton form={form} isFirstRunMode={isFirstRunMode} />
  </Box>
);

type LoginActionFormProps = {
  form: LoginFormState;
};

const TrustDeviceButton = ({ form }: LoginActionFormProps) => {
  const { t } = useTranslation("login");

  return (
    <TrustDeviceToggle
      active={form.trustDevice}
      onToggle={form.toggleTrustDevice}
      disabled={form.loading || form.disabled}
      tooltip={t("rememberMe")}
    />
  );
};

const PasskeyLoginButton = ({ form }: LoginActionFormProps) => {
  const { t } = useTranslation("login");

  return (
    <Tooltip title={t("passkey.loginButton")}>
      <span>
        <IconButton
          type="button"
          color="primary"
          onClick={form.handlePasskeyLogin}
          disabled={form.loading || form.passkeyLoading || form.disabled}
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
  );
};

type LoginSubmitButtonProps = {
  form: LoginFormState;
  isFirstRunMode: boolean;
};

const LoginSubmitButton = ({
  form,
  isFirstRunMode,
}: LoginSubmitButtonProps) => {
  const { t } = useTranslation("login");
  const submitButtonLabel = isFirstRunMode
    ? t("firstRun.continueButton")
    : t("loginButton");

  return (
    <Button
      type="submit"
      variant="contained"
      color="primary"
      disabled={form.loading || form.disabled}
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
          gap: form.loading ? 0 : 1,
        }}
      >
        <SubmitLabel loading={form.loading} label={submitButtonLabel} />
        <SubmitIcon loading={form.loading} />
      </Box>
    </Button>
  );
};

type SubmitLabelProps = {
  loading: boolean;
  label: string;
};

const SubmitLabel = ({ loading, label }: SubmitLabelProps) => (
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
    {label}
  </Box>
);

type SubmitIconProps = {
  loading: boolean;
};

const SubmitIcon = ({ loading }: SubmitIconProps) => (
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
      <CircularProgress color="inherit" size={18} thickness={5} />
    ) : (
      <KeyboardArrowRight />
    )}
  </Box>
);

type LoginForgotPasswordLinkProps = {
  form: LoginFormState;
  show: boolean;
};

const LoginForgotPasswordLink = ({
  form,
  show,
}: LoginForgotPasswordLinkProps) => {
  const { t } = useTranslation("login");

  return show ? (
    <ForgotPasswordLink
      onClick={form.handleForgotPassword}
      disabled={form.loading || form.forgotPasswordSending || form.disabled}
      label={
        form.forgotPasswordSending
          ? t("forgotPassword.sending")
          : t("forgotPassword.link")
      }
    />
  ) : null;
};
