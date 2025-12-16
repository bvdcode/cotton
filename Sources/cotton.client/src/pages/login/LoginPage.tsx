import {
  Box,
  Paper,
  Button,
  TextField,
  Container,
  Typography,
} from "@mui/material";
import { useAuth } from "../../features/auth";
import { useTranslation } from "react-i18next";
import { useState, type FormEvent } from "react";
import { authApi } from "../../shared/api/authApi";
import { useNavigate, useLocation, Navigate } from "react-router-dom";

export const LoginPage = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { t } = useTranslation("login");
  const [error, setError] = useState("");
  const { isAuthenticated, isInitializing, setAuthenticated } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  // Redirect to home if already authenticated
  if (!isInitializing && isAuthenticated) {
    const from = (location.state as { from?: string })?.from || "/";
    return <Navigate to={from} replace />;
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);

    try {
      await authApi.login({ username, password });
      const user = await authApi.me();
      setAuthenticated(true, user);
      navigate("/");
    } catch (err) {
      setError(t("errorMessage"));
      console.error("Login error:", err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Container maxWidth="sm">
      <Paper
        sx={{
          mt: 8,
          p: 4,
          boxShadow: 3,
          borderRadius: 2,
        }}
      >
        <Typography variant="h4" component="h1" gutterBottom>
          {t("title")}
        </Typography>
        <Box
          component="form"
          onSubmit={handleSubmit}
          noValidate
          autoComplete="off"
        >
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
          {error && (
            <Typography color="error" sx={{ mt: 2 }}>
              {error}
            </Typography>
          )}
          <Button
            type="submit"
            variant="contained"
            color="primary"
            disabled={loading}
            sx={{ mt: 2 }}
          >
            {loading ? t("loggingIn") : t("loginButton")}
          </Button>
        </Box>
      </Paper>
    </Container>
  );
};
