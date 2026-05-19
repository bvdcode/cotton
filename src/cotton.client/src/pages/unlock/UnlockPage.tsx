import {
  Alert,
  Avatar,
  Box,
  Button,
  CircularProgress,
  Container,
  IconButton,
  InputAdornment,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import {
  ContentCopy,
  Key,
  LockOpen,
  Visibility,
  VisibilityOff,
} from "@mui/icons-material";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { unlockApi, type UnlockStatusResponse } from "../../shared/api/unlockApi";
import { toast } from "@shared/ui/notifications";

const masterKeyLength = 32;

type UnlockPageProps = {
  initialStatus?: UnlockStatusResponse;
};

export const UnlockPage = ({ initialStatus }: UnlockPageProps) => {
  const { t } = useTranslation("unlock");
  const [status, setStatus] = useState<UnlockStatusResponse | null>(
    initialStatus ?? null,
  );
  const [loaded, setLoaded] = useState(initialStatus !== undefined);
  const [masterKey, setMasterKey] = useState("");
  const [bootstrapToken, setBootstrapToken] = useState("");
  const [showMasterKey, setShowMasterKey] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    if (initialStatus !== undefined) {
      return;
    }

    let cancelled = false;

    unlockApi
      .getStatus()
      .then((nextStatus) => {
        if (cancelled) return;
        setStatus(nextStatus);
      })
      .catch(() => {
        if (cancelled) return;
        setStatus(null);
      })
      .finally(() => {
        if (!cancelled) {
          setLoaded(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [initialStatus]);

  const requiresBootstrapToken = status?.requiresBootstrapToken === true;
  const firstUnlockExpiresAtUtc = status?.firstUnlockExpiresAtUtc ?? null;
  const expiresAt = useMemo(() => {
    if (!firstUnlockExpiresAtUtc) {
      return null;
    }

    try {
      return new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "medium",
      }).format(new Date(firstUnlockExpiresAtUtc));
    } catch {
      return firstUnlockExpiresAtUtc;
    }
  }, [firstUnlockExpiresAtUtc]);

  if (loaded && status === null) {
    return <Navigate to="/" replace />;
  }

  const canSubmit =
    masterKey.trim().length === masterKeyLength &&
    (!requiresBootstrapToken || bootstrapToken.trim().length > 0) &&
    !submitting;

  const handleGenerate = async () => {
    setGenerating(true);
    setError(null);
    setSuccess(null);
    try {
      const key = await unlockApi.generateKey();
      setMasterKey(key);
      setShowMasterKey(true);
      setSuccess(t("generated"));
      toast.success(t("generatedToast"), { toastId: "unlock-generated" });
    } catch (err) {
      const message = err instanceof Error ? err.message : t("generateFailed");
      setError(message);
      toast.error(message, { toastId: "unlock-generate-failed" });
    } finally {
      setGenerating(false);
    }
  };

  const handleCopy = async () => {
    if (!masterKey) return;
    try {
      await navigator.clipboard.writeText(masterKey);
      toast.success(t("copied"), { toastId: "unlock-copy" });
    } catch {
      toast.error(t("copyFailed"), { toastId: "unlock-copy-failed" });
    }
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) return;

    setSubmitting(true);
    setError(null);
    setSuccess(null);
    try {
      const response = await unlockApi.unlock({
        masterKey: masterKey.trim(),
        bootstrapToken: bootstrapToken.trim(),
      });
      setMasterKey("");
      setBootstrapToken("");
      setSuccess(response.message || t("unlocked"));
      toast.success(response.message || t("unlocked"), {
        toastId: "unlock-success",
      });
      await unlockApi.waitUntilAppReady();
      try {
        window.sessionStorage.setItem(
          "cotton:just-unlocked",
          Date.now().toString(),
        );
      } catch {
        // Session storage can be unavailable in strict privacy modes.
      }
      window.location.replace("/");
    } catch (err) {
      const message = err instanceof Error ? err.message : t("unlockFailed");
      setError(message);
      toast.error(message, { toastId: "unlock-failed" });
      setSubmitting(false);
    }
  };

  return (
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
        <Paper sx={{ p: { xs: 3, sm: 4 } }}>
          <Box
            display="flex"
            justifyContent="space-between"
            alignItems="center"
            sx={{ mb: 3 }}
          >
            <Box>
              <Typography variant="h4" component="h1">
                {t("title")}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75 }}>
                {t("caption")}
              </Typography>
            </Box>
            <Avatar src="/assets/icons/icon.svg" alt="Cotton" />
          </Box>

          <Box component="form" onSubmit={handleSubmit} noValidate autoComplete="off">
            <Stack spacing={2.5}>
              {requiresBootstrapToken && (
                <Alert severity="warning">
                  {expiresAt
                    ? t("bootstrapRequiredWithExpiry", { expiresAt })
                    : t("bootstrapRequired")}
                </Alert>
              )}

              {error && <Alert severity="error">{error}</Alert>}
              {success && <Alert severity="success">{success}</Alert>}

              <TextField
                label={t("masterKey")}
                value={masterKey}
                onChange={(event) => setMasterKey(event.target.value)}
                disabled={submitting}
                type={showMasterKey ? "text" : "password"}
                required
                autoComplete="off"
                slotProps={{
                  htmlInput: {
                    maxLength: masterKeyLength,
                    spellCheck: false,
                  },
                  input: {
                    endAdornment: (
                      <InputAdornment position="end">
                        <Tooltip title={generating ? t("generating") : t("generate")}>
                          <span>
                            <IconButton
                              aria-label={t("generate")}
                              edge="end"
                              onClick={handleGenerate}
                              disabled={generating || submitting}
                            >
                              {generating ? (
                                <CircularProgress color="inherit" size={18} thickness={5} />
                              ) : (
                                <Key />
                              )}
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title={showMasterKey ? t("hideKey") : t("showKey")}>
                          <IconButton
                            aria-label={showMasterKey ? t("hideKey") : t("showKey")}
                            edge="end"
                            onClick={() => setShowMasterKey((value) => !value)}
                            disabled={submitting}
                          >
                            {showMasterKey ? <VisibilityOff /> : <Visibility />}
                          </IconButton>
                        </Tooltip>
                        <Tooltip title={t("copyKey")}>
                          <span>
                            <IconButton
                              aria-label={t("copyKey")}
                              edge="end"
                              onClick={handleCopy}
                              disabled={!masterKey || submitting}
                            >
                              <ContentCopy />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </InputAdornment>
                    ),
                  },
                }}
              />

              {requiresBootstrapToken && (
                <TextField
                  label={t("bootstrapToken")}
                  value={bootstrapToken}
                  onChange={(event) => setBootstrapToken(event.target.value)}
                  disabled={submitting}
                  type="password"
                  required
                  autoComplete="off"
                  slotProps={{
                    htmlInput: {
                      spellCheck: false,
                    },
                  }}
                />
              )}

              <Box
                sx={{
                  display: "flex",
                  gap: 1,
                  justifyContent: "center",
                  alignItems: "center",
                  flexWrap: "wrap",
                }}
              >
                <Button
                  type="submit"
                  variant="contained"
                  color="primary"
                  disabled={!canSubmit}
                  startIcon={
                    submitting ? (
                      <CircularProgress color="inherit" size={18} thickness={5} />
                    ) : (
                      <LockOpen />
                    )
                  }
                >
                  {submitting ? t("unlocking") : t("unlock")}
                </Button>
              </Box>
            </Stack>
          </Box>
        </Paper>
      </Container>
    </Box>
  );
};
