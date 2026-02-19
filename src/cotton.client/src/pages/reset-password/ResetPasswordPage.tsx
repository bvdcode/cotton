import {
  Box,
  Paper,
  Button,
  TextField,
  Container,
  Typography,
  Avatar,
  Alert,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useState, useCallback, type FormEvent } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { authApi } from "../../shared/api/authApi";
import axios from "axios";

export const ResetPasswordPage = () => {
  const { t } = useTranslation("resetPassword");
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = searchParams.get("token") ?? "";

  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      setError("");

      if (newPassword !== confirmPassword) {
        setError(t("errors.mismatch"));
        return;
      }

      if (!token) {
        setError(t("errors.invalidToken"));
        return;
      }

      setLoading(true);
      try {
        await authApi.resetPassword(token, newPassword);
        setSuccess(true);
      } catch (err) {
        if (axios.isAxiosError(err)) {
          const status = err.response?.status;
          if (status === 400) {
            setError(t("errors.invalidToken"));
            return;
          }
        }
        setError(t("errors.failed"));
      } finally {
        setLoading(false);
      }
    },
    [newPassword, confirmPassword, token, t],
  );

  const handleGoToLogin = useCallback(() => {
    navigate("/login");
  }, [navigate]);

  return (
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
            {t("title")}
          </Typography>
          <Avatar src="/assets/icons/icon.svg" alt="App Logo" />
        </Box>

        {success ? (
          <Box>
            <Alert color="success" sx={{ mt: 2 }}>
              {t("success")}
            </Alert>
            <Button
              variant="contained"
              color="primary"
              fullWidth
              onClick={handleGoToLogin}
              sx={{ mt: 3 }}
            >
              {t("goToLogin")}
            </Button>
          </Box>
        ) : (
          <Box
            component="form"
            onSubmit={handleSubmit}
            noValidate
            autoComplete="off"
          >
            <TextField
              fullWidth
              label={t("newPassword")}
              type="password"
              margin="normal"
              variant="outlined"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={loading}
            />
            <TextField
              fullWidth
              label={t("confirmPassword")}
              type="password"
              margin="normal"
              variant="outlined"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              disabled={loading}
            />
            {error && (
              <Alert color="error" sx={{ mt: 2 }}>
                {error}
              </Alert>
            )}
            <Button
              type="submit"
              variant="contained"
              color="primary"
              disabled={loading || !newPassword || !confirmPassword}
              fullWidth
              sx={{ mt: 3 }}
            >
              {loading ? t("submitting") : t("submit")}
            </Button>
          </Box>
        )}
      </Paper>
    </Container>
  );
};
