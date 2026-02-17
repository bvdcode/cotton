import {
  Box,
  Paper,
  Button,
  Container,
  Typography,
  Avatar,
  Alert,
  CircularProgress,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useState, useEffect, useRef, useCallback } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { authApi } from "../../shared/api/authApi";
import { useAuth } from "../../features/auth";
import axios from "axios";

export const VerifyEmailPage = () => {
  const { t } = useTranslation("verifyEmail");
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const token = searchParams.get("token") ?? "";

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const verifiedRef = useRef(false);

  useEffect(() => {
    if (!token || verifiedRef.current) {
      setLoading(false);
      setError(t("errors.invalidToken"));
      return;
    }

    verifiedRef.current = true;

    const verifyEmail = async () => {
      try {
        await authApi.confirmEmailVerification(token);
        setSuccess(true);
      } catch (err) {
        if (axios.isAxiosError(err) && err.response?.status === 400) {
          setError(t("errors.invalidToken"));
        } else {
          setError(t("errors.failed"));
        }
      } finally {
        setLoading(false);
      }
    };

    verifyEmail();
  }, [token, t]);

  const handleNavigate = useCallback(() => {
    navigate(isAuthenticated ? "/profile" : "/login");
  }, [navigate, isAuthenticated]);

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

        {loading && (
          <Box display="flex" alignItems="center" gap={2} sx={{ mt: 2 }}>
            <CircularProgress size={24} />
            <Typography variant="body1" color="text.secondary">
              {t("verifying")}
            </Typography>
          </Box>
        )}

        {success && (
          <Box>
            <Alert color="success" sx={{ mt: 2 }}>
              {t("success")}
            </Alert>
            <Button
              variant="contained"
              color="primary"
              fullWidth
              onClick={handleNavigate}
              sx={{ mt: 3 }}
            >
              {isAuthenticated ? t("goToProfile") : t("goToLogin")}
            </Button>
          </Box>
        )}

        {error && !loading && (
          <Box>
            <Alert color="error" sx={{ mt: 2 }}>
              {error}
            </Alert>
            <Button
              variant="contained"
              color="primary"
              fullWidth
              onClick={handleNavigate}
              sx={{ mt: 3 }}
            >
              {isAuthenticated ? t("goToProfile") : t("goToLogin")}
            </Button>
          </Box>
        )}
      </Paper>
    </Container>
  );
};
