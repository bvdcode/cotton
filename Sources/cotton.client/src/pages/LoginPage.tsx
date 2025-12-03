import {
  Box,
  Card,
  Stack,
  Alert,
  Avatar,
  Button,
  TextField,
  Typography,
  CardContent,
  IconButton,
  CircularProgress,
} from "@mui/material";
import React, { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Visibility, VisibilityOff } from "@mui/icons-material";
import { AuthError, useAuthStore, type ReactKitProps } from "@bvdcode/react-kit";

type Props = {
  appProps: ReactKitProps;
};

const LoginPage: React.FC<Props> = ({ appProps }) => {
  const { t } = useTranslation();
  const login = useAuthStore((s) => s.login);
  const apiService = useAuthStore((s) => s.apiService);

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [showValidation, setShowValidation] = useState(false);

  // Check if onLogin handler is configured
  const isConfigError = !apiService;

  // Username validation using external regex if provided
  const usernamePattern = appProps.authConfig?.usernamePattern;
  const isUsernameInvalid = useMemo(() => {
    if (!usernamePattern || username.length === 0) return false;
    // Avoid stateful global regex issues
    if ((usernamePattern as RegExp).global) {
      (usernamePattern as RegExp).lastIndex = 0;
    }
    return !usernamePattern.test(username);
  }, [usernamePattern, username]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Perform validation only on submit
    if (usernamePattern) {
      if ((usernamePattern as RegExp).global) {
        (usernamePattern as RegExp).lastIndex = 0;
      }
      const invalid = !usernamePattern.test(username);
      if (invalid) {
        setShowValidation(true);
        return;
      }
    }

    setLoading(true);
    try {
      await login({ username, password });
    } catch (err) {
      if (err instanceof AuthError) {
        setError(err.message);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError(t("login.unknownError"));
      }
    } finally {
      setLoading(false);
    }
  };

  const handleClickShowPassword = () => {
    setShowPassword(!showPassword);
  };

  const handleMouseDownPassword = (
    event: React.MouseEvent<HTMLButtonElement>,
  ) => {
    event.preventDefault();
  };

  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100vh",
        width: "100vw",
        bgcolor: "background.default",
        p: 2,
      }}
    >
      <Card sx={{ maxWidth: 400, width: "100%" }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={3} alignItems="center">
            <Avatar
              src={appProps.logoUrl}
              alt={appProps.appName}
              sx={{
                width: 80,
                height: 80,
                transition: "transform 0.2s ease-in-out",
                ":hover": { transform: "scale(1.02)" },
              }}
            />
            <Typography variant="h5" component="div" fontWeight="bold">
              {appProps.appName}
            </Typography>
          </Stack>

          {isConfigError && (
            <Alert severity="error" sx={{ mt: 3 }}>
              {t("login.configError")}
            </Alert>
          )}

          {error && (
            <Alert severity="error" sx={{ mt: 3 }}>
              {error}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit} sx={{ mt: 3 }}>
            <Stack spacing={2}>
              <TextField
                label={t("login.username")}
                fullWidth
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                disabled={loading || isConfigError}
                error={showValidation && isUsernameInvalid}
                helperText={
                  showValidation && isUsernameInvalid
                    ? t("login.usernameInvalid")
                    : undefined
                }
                autoFocus
                autoComplete="username"
                required
              />
              <TextField
                label={t("login.password")}
                type={showPassword ? "text" : "password"}
                fullWidth
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={loading || isConfigError}
                autoComplete="current-password"
                required
                slotProps={{
                  input: {
                    endAdornment: (
                      <IconButton
                        aria-label="toggle password visibility"
                        onClick={handleClickShowPassword}
                        onMouseDown={handleMouseDownPassword}
                        edge="end"
                      >
                        {showPassword ? <VisibilityOff /> : <Visibility />}
                      </IconButton>
                    ),
                  },
                }}
              />
              <Button
                type="submit"
                variant="contained"
                fullWidth
                size="large"
                disabled={loading || isConfigError}
              >
                {loading ? (
                  <CircularProgress size={24} color="inherit" />
                ) : (
                  t("login.signIn")
                )}
              </Button>
            </Stack>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
};

export default LoginPage;
