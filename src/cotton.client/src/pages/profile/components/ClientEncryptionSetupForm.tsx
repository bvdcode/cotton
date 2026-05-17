import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  DialogActions,
  DialogContent,
  FormControlLabel,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import DownloadIcon from "@mui/icons-material/Download";
import VisibilityIcon from "@mui/icons-material/Visibility";
import VisibilityOffIcon from "@mui/icons-material/VisibilityOff";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import type { UserPreferences } from "../../../shared/api/userPreferencesApi";
import { persistEnvelope, setupEnvelope, useVault } from "../../../shared/crypto";

const MIN_PASSWORD_LENGTH = 10;

type SetupStep = "warning" | "password" | "phrase";

type ClientEncryptionSetupFormProps = {
  onCancel: () => void;
  onSuccess: (preferences: UserPreferences) => void;
};

export const ClientEncryptionSetupForm = ({
  onCancel,
  onSuccess,
}: ClientEncryptionSetupFormProps) => {
  const { t } = useTranslation("profile");
  const unlockVault = useVault((state) => state.unlock);

  const [step, setStep] = useState<SetupStep>("warning");
  const [acknowledged, setAcknowledged] = useState(false);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [phrase, setPhrase] = useState<string | null>(null);
  const [envelope, setEnvelope] = useState<Uint8Array | null>(null);
  const [masterKey, setMasterKey] = useState<CryptoKey | null>(null);
  const [phraseStored, setPhraseStored] = useState(false);
  const [phraseCopied, setPhraseCopied] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const phraseWords = useMemo(() => phrase?.split(" ") ?? [], [phrase]);
  const passwordTooShort =
    password.length > 0 && password.length < MIN_PASSWORD_LENGTH;
  const passwordMismatch =
    confirmPassword.length > 0 && confirmPassword !== password;
  const canGenerate =
    password.length >= MIN_PASSWORD_LENGTH &&
    password === confirmPassword &&
    !pending;

  const handleGenerate = async () => {
    setError(null);
    setPending(true);

    try {
      const result = await setupEnvelope(password);
      setPhrase(result.recoveryPhrase);
      setEnvelope(result.envelope);
      setMasterKey(result.masterKey);
      setStep("phrase");
    } catch (error) {
      setError(
        getApiErrorMessage(error) ??
          t("clientEncryption.setupDialog.errors.generateFailed"),
      );
    } finally {
      setPending(false);
    }
  };

  const handleCopyPhrase = async () => {
    if (!phrase) {
      return;
    }

    try {
      await navigator.clipboard.writeText(phrase);
      setPhraseCopied(true);
    } catch {
      setError(t("clientEncryption.setupDialog.errors.copyFailed"));
    }
  };

  const handleDownloadPhrase = () => {
    if (!phrase) {
      return;
    }

    try {
      const blob = new Blob([`${phrase}\n`], {
        type: "text/plain;charset=utf-8",
      });
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement("a");

      link.href = objectUrl;
      link.download = "cotton-client-encryption-backup-phrase.txt";
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
    } catch {
      setError(t("clientEncryption.setupDialog.errors.downloadFailed"));
    }
  };

  const handleFinish = async () => {
    if (!envelope || !masterKey) {
      return;
    }

    setError(null);
    setPending(true);

    try {
      const preferences = await persistEnvelope(envelope);
      unlockVault(masterKey);
      onSuccess(preferences);
    } catch (error) {
      setError(
        getApiErrorMessage(error) ??
          t("clientEncryption.setupDialog.errors.saveFailed"),
      );
    } finally {
      setPending(false);
    }
  };

  if (step === "warning") {
    return (
      <>
        <DialogContent dividers>
          <Stack spacing={2}>
            <Alert severity="warning">
              {t("clientEncryption.setupDialog.warning")}
            </Alert>
            <FormControlLabel
              control={
                <Checkbox
                  checked={acknowledged}
                  onChange={(_, checked) => setAcknowledged(checked)}
                />
              }
              label={t("clientEncryption.setupDialog.acknowledge")}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onCancel}>
            {t("clientEncryption.setupDialog.cancel")}
          </Button>
          <Button
            variant="contained"
            disabled={!acknowledged}
            onClick={() => setStep("password")}
          >
            {t("clientEncryption.setupDialog.continue")}
          </Button>
        </DialogActions>
      </>
    );
  }

  if (step === "password") {
    const visibilityLabel = passwordVisible
      ? t("password.hide")
      : t("password.show");

    return (
      <>
        <DialogContent dividers>
          <Stack spacing={2} pt={0.5}>
            <Typography variant="body2" color="text.secondary">
              {t("clientEncryption.setupDialog.passwordHint")}
            </Typography>
            <TextField
              label={t("clientEncryption.setupDialog.passwordLabel")}
              type={passwordVisible ? "text" : "password"}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              error={passwordTooShort}
              helperText={
                passwordTooShort
                  ? t("clientEncryption.setupDialog.passwordTooShort", {
                      min: MIN_PASSWORD_LENGTH,
                    })
                  : " "
              }
              autoComplete="new-password"
              fullWidth
              disabled={pending}
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <Tooltip title={visibilityLabel}>
                        <IconButton
                          edge="end"
                          aria-label={visibilityLabel}
                          onClick={() => setPasswordVisible((value) => !value)}
                          disabled={pending}
                        >
                          {passwordVisible ? (
                            <VisibilityOffIcon />
                          ) : (
                            <VisibilityIcon />
                          )}
                        </IconButton>
                      </Tooltip>
                    </InputAdornment>
                  ),
                },
              }}
            />
            <TextField
              label={t("clientEncryption.setupDialog.confirmPasswordLabel")}
              type={passwordVisible ? "text" : "password"}
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              error={passwordMismatch}
              helperText={
                passwordMismatch
                  ? t("clientEncryption.setupDialog.passwordMismatch")
                  : " "
              }
              autoComplete="new-password"
              fullWidth
              disabled={pending}
              onKeyDown={(event) => {
                if (event.key === "Enter" && canGenerate) {
                  void handleGenerate();
                }
              }}
            />
            {error && <Alert severity="error">{error}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStep("warning")} disabled={pending}>
            {t("clientEncryption.setupDialog.back")}
          </Button>
          <Box sx={{ flex: 1 }} />
          <Button onClick={onCancel} disabled={pending}>
            {t("clientEncryption.setupDialog.cancel")}
          </Button>
          <Button
            variant="contained"
            disabled={!canGenerate}
            onClick={handleGenerate}
          >
            {pending ? (
              <Stack direction="row" spacing={1} alignItems="center">
                <CircularProgress size={16} />
                <span>{t("clientEncryption.setupDialog.generating")}</span>
              </Stack>
            ) : (
              t("clientEncryption.setupDialog.next")
            )}
          </Button>
        </DialogActions>
      </>
    );
  }

  return (
    <>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity="warning">
            {t("clientEncryption.setupDialog.phraseWarning")}
          </Alert>
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "repeat(2, minmax(0, 1fr))",
                sm: "repeat(3, minmax(0, 1fr))",
              },
              gap: 1,
              p: 2,
              borderRadius: 1,
              bgcolor: "action.hover",
              fontFamily: "monospace",
            }}
          >
            {phraseWords.map((word, index) => (
              <Box
                key={`${word}-${index}`}
                sx={{
                  display: "flex",
                  gap: 1,
                  minWidth: 0,
                  alignItems: "baseline",
                }}
              >
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ minWidth: 24, fontFamily: "monospace" }}
                >
                  {index + 1}.
                </Typography>
                <Typography
                  variant="body2"
                  sx={{ fontFamily: "monospace", overflowWrap: "anywhere" }}
                >
                  {word}
                </Typography>
              </Box>
            ))}
          </Box>
          <Stack direction="row" spacing={1} alignItems="center">
            <Button
              size="small"
              startIcon={<ContentCopyIcon fontSize="small" />}
              onClick={handleCopyPhrase}
              disabled={!phrase}
            >
              {phraseCopied
                ? t("clientEncryption.setupDialog.copied")
                : t("clientEncryption.setupDialog.copy")}
            </Button>
            <Button
              size="small"
              startIcon={<DownloadIcon fontSize="small" />}
              onClick={handleDownloadPhrase}
              disabled={!phrase}
            >
              {t("clientEncryption.setupDialog.download")}
            </Button>
          </Stack>
          <FormControlLabel
            control={
              <Checkbox
                checked={phraseStored}
                onChange={(_, checked) => setPhraseStored(checked)}
                disabled={pending}
              />
            }
            label={t("clientEncryption.setupDialog.phraseStored")}
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={pending}>
          {t("clientEncryption.setupDialog.cancel")}
        </Button>
        <Button
          variant="contained"
          disabled={!phraseStored || pending}
          onClick={handleFinish}
        >
          {pending ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <CircularProgress size={16} />
              <span>{t("clientEncryption.setupDialog.saving")}</span>
            </Stack>
          ) : (
            t("clientEncryption.setupDialog.finish")
          )}
        </Button>
      </DialogActions>
    </>
  );
};
