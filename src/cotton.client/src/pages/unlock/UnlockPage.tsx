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
  ToggleButton,
  ToggleButtonGroup,
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
import { JUST_UNLOCKED_STORAGE_KEY } from "../../features/auth/authStorageKeys";
import {
  recoveryPhraseToKdfSecret,
  validateRecoveryPhrase,
} from "../../shared/crypto/recoveryKey";

const masterKeyLength = 32;
type UnlockMode = "master-key" | "recovery-phrase";

type UnlockPageProps = {
  initialStatus?: UnlockStatusResponse;
};

type UnlockFormState = ReturnType<typeof useUnlockFormState>;

const useUnlockStatus = (
  initialStatus?: UnlockStatusResponse,
): { loaded: boolean; status: UnlockStatusResponse | null } => {
  const [status, setStatus] = useState<UnlockStatusResponse | null>(
    initialStatus ?? null,
  );
  const [loaded, setLoaded] = useState(initialStatus !== undefined);

  useEffect(() => {
    if (initialStatus !== undefined) {
      return;
    }

    let cancelled = false;

    unlockApi
      .getStatus()
      .then((nextStatus) => {
        if (!cancelled) {
          setStatus(nextStatus);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setStatus(null);
        }
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

  return { loaded, status };
};

const useFormattedExpiry = (status: UnlockStatusResponse | null): string | null => {
  const firstUnlockExpiresAtUtc = status?.firstUnlockExpiresAtUtc ?? null;

  return useMemo(() => {
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
};

const useUnlockFormState = (status: UnlockStatusResponse | null) => {
  const { t } = useTranslation("unlock");
  const [mode, setMode] = useState<UnlockMode>("master-key");
  const [masterKey, setMasterKey] = useState("");
  const [bootstrapToken, setBootstrapToken] = useState("");
  const [showMasterKey, setShowMasterKey] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const requiresBootstrapToken = status?.requiresBootstrapToken === true;
  const trimmedUnlockInput = masterKey.trim();
  const unlockInputLooksValid = mode === "recovery-phrase"
    ? validateRecoveryPhrase(trimmedUnlockInput)
    : trimmedUnlockInput.length === masterKeyLength;
  const canSubmit =
    unlockInputLooksValid &&
    (!requiresBootstrapToken || bootstrapToken.trim().length > 0) &&
    !submitting;

  const resetMessages = () => {
    setError(null);
    setSuccess(null);
  };

  const handleModeChange = (_: unknown, nextMode: UnlockMode | null) => {
    if (!nextMode || nextMode === mode || submitting) {
      return;
    }

    setMode(nextMode);
    setMasterKey("");
    setShowMasterKey(false);
    resetMessages();
  };

  const handleGenerate = async () => {
    setGenerating(true);
    resetMessages();
    try {
      const key = await unlockApi.generateKey();
      setMode("master-key");
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
    resetMessages();
    try {
      let unlockSecret = masterKey.trim();
      if (mode === "recovery-phrase") {
        try {
          unlockSecret = recoveryPhraseToKdfSecret(unlockSecret);
        } catch {
          setError(t("invalidRecoveryPhrase"));
          setSubmitting(false);
          return;
        }
      }

      const response = await unlockApi.unlock({
        masterKey: unlockSecret,
        bootstrapToken: bootstrapToken.trim(),
      });
      setMasterKey("");
      setBootstrapToken("");
      setSuccess(response.message || t("unlocked"));
      toast.success(response.message || t("unlocked"), {
        toastId: "unlock-success",
      });
      await unlockApi.waitUntilAppReady();
      rememberJustUnlocked();
      window.location.replace("/");
    } catch (err) {
      const message = err instanceof Error ? err.message : t("unlockFailed");
      setError(message);
      toast.error(message, { toastId: "unlock-failed" });
      setSubmitting(false);
    }
  };

  return {
    bootstrapToken,
    canSubmit,
    error,
    generating,
    handleCopy,
    handleGenerate,
    handleModeChange,
    handleSubmit,
    masterKey,
    mode,
    requiresBootstrapToken,
    setBootstrapToken,
    setMasterKey,
    setShowMasterKey,
    showMasterKey,
    submitting,
    success,
  };
};

const rememberJustUnlocked = (): void => {
  try {
    window.sessionStorage.setItem(
      JUST_UNLOCKED_STORAGE_KEY,
      Date.now().toString(),
    );
  } catch {
    // Session storage can be unavailable in strict privacy modes.
  }
};

const UnlockHeader = (): React.ReactElement => {
  const { t } = useTranslation("unlock");

  return (
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
  );
};

const UnlockModeSelector = ({
  form,
}: {
  form: UnlockFormState;
}): React.ReactElement => {
  const { t } = useTranslation("unlock");

  return (
    <ToggleButtonGroup
      value={form.mode}
      exclusive
      fullWidth
      size="small"
      onChange={form.handleModeChange}
      aria-label={t("mode.label")}
      disabled={form.submitting}
    >
      <ToggleButton value="master-key">{t("mode.masterKey")}</ToggleButton>
      <ToggleButton value="recovery-phrase">{t("mode.recoveryPhrase")}</ToggleButton>
    </ToggleButtonGroup>
  );
};

const MasterKeyField = ({
  form,
}: {
  form: UnlockFormState;
}): React.ReactElement => {
  const { t } = useTranslation("unlock");

  if (form.mode === "recovery-phrase") {
    return (
      <TextField
        label={t("recoveryPhrase")}
        value={form.masterKey}
        onChange={(event) => form.setMasterKey(event.target.value)}
        disabled={form.submitting}
        required
        autoComplete="off"
        multiline
        minRows={3}
        placeholder={t("recoveryPhrasePlaceholder")}
        slotProps={{
          htmlInput: {
            spellCheck: false,
          },
        }}
      />
    );
  }

  return (
    <TextField
      label={t("masterKey")}
      value={form.masterKey}
      onChange={(event) => form.setMasterKey(event.target.value)}
      disabled={form.submitting}
      type={form.showMasterKey ? "text" : "password"}
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
              <Tooltip title={form.generating ? t("generating") : t("generate")}>
                <span>
                  <IconButton
                    aria-label={t("generate")}
                    edge="end"
                    onClick={form.handleGenerate}
                    disabled={form.generating || form.submitting}
                  >
                    {form.generating ? (
                      <CircularProgress color="inherit" size={18} thickness={5} />
                    ) : (
                      <Key />
                    )}
                  </IconButton>
                </span>
              </Tooltip>
              <Tooltip title={form.showMasterKey ? t("hideKey") : t("showKey")}>
                <IconButton
                  aria-label={form.showMasterKey ? t("hideKey") : t("showKey")}
                  edge="end"
                  onClick={() => form.setShowMasterKey((value) => !value)}
                  disabled={form.submitting}
                >
                  {form.showMasterKey ? <VisibilityOff /> : <Visibility />}
                </IconButton>
              </Tooltip>
              <Tooltip title={t("copyKey")}>
                <span>
                  <IconButton
                    aria-label={t("copyKey")}
                    edge="end"
                    onClick={form.handleCopy}
                    disabled={!form.masterKey || form.submitting}
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
  );
};

const BootstrapTokenField = ({
  form,
}: {
  form: UnlockFormState;
}): React.ReactElement => {
  const { t } = useTranslation("unlock");

  return (
    <TextField
      label={t("bootstrapToken")}
      value={form.bootstrapToken}
      onChange={(event) => form.setBootstrapToken(event.target.value)}
      disabled={form.submitting}
      type="password"
      required
      autoComplete="off"
      slotProps={{
        htmlInput: {
          spellCheck: false,
        },
      }}
    />
  );
};

const UnlockSubmitButton = ({
  form,
}: {
  form: UnlockFormState;
}): React.ReactElement => {
  const { t } = useTranslation("unlock");

  return (
    <Button
      type="submit"
      variant="contained"
      color="primary"
      disabled={!form.canSubmit}
      startIcon={
        form.submitting ? (
          <CircularProgress color="inherit" size={18} thickness={5} />
        ) : (
          <LockOpen />
        )
      }
    >
      {form.submitting ? t("unlocking") : t("unlock")}
    </Button>
  );
};

const UnlockForm = ({
  expiresAt,
  form,
}: {
  expiresAt: string | null;
  form: UnlockFormState;
}): React.ReactElement => {
  const { t } = useTranslation("unlock");

  return (
    <Box component="form" onSubmit={form.handleSubmit} noValidate autoComplete="off">
      <Stack spacing={2.5}>
        {form.requiresBootstrapToken && (
          <Alert severity="warning">
            {expiresAt
              ? t("bootstrapRequiredWithExpiry", { expiresAt })
              : t("bootstrapRequired")}
          </Alert>
        )}
        {form.error && <Alert severity="error">{form.error}</Alert>}
        {form.success && <Alert severity="success">{form.success}</Alert>}
        <UnlockModeSelector form={form} />
        <MasterKeyField form={form} />
        {form.requiresBootstrapToken && <BootstrapTokenField form={form} />}
        <Box
          sx={{
            display: "flex",
            gap: 1,
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "wrap",
          }}
        >
          <UnlockSubmitButton form={form} />
        </Box>
      </Stack>
    </Box>
  );
};

export const UnlockPage = ({ initialStatus }: UnlockPageProps) => {
  const { loaded, status } = useUnlockStatus(initialStatus);
  const expiresAt = useFormattedExpiry(status);
  const form = useUnlockFormState(status);

  if (loaded && status === null) {
    return <Navigate to="/" replace />;
  }

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
          <UnlockHeader />
          <UnlockForm expiresAt={expiresAt} form={form} />
        </Paper>
      </Container>
    </Box>
  );
};
