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
import { useTranslation } from "react-i18next";

export const MIN_PASSWORD_LENGTH = 10;

type WarningStepProps = {
  acknowledged: boolean;
  onAcknowledgeChange: (acknowledged: boolean) => void;
  onCancel: () => void;
  onContinue: () => void;
};

export const WarningStep = ({
  acknowledged,
  onAcknowledgeChange,
  onCancel,
  onContinue,
}: WarningStepProps) => {
  const { t } = useTranslation("profile");

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
                onChange={(_, checked) => onAcknowledgeChange(checked)}
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
          onClick={onContinue}
        >
          {t("clientEncryption.setupDialog.continue")}
        </Button>
      </DialogActions>
    </>
  );
};

type PasswordStepProps = {
  password: string;
  confirmPassword: string;
  passwordVisible: boolean;
  passwordTooShort: boolean;
  passwordMismatch: boolean;
  canGenerate: boolean;
  pending: boolean;
  error: string | null;
  onPasswordChange: (value: string) => void;
  onConfirmPasswordChange: (value: string) => void;
  onPasswordVisibilityToggle: () => void;
  onBack: () => void;
  onCancel: () => void;
  onGenerate: () => void;
};

export const PasswordStep = ({
  password,
  confirmPassword,
  passwordVisible,
  passwordTooShort,
  passwordMismatch,
  canGenerate,
  pending,
  error,
  onPasswordChange,
  onConfirmPasswordChange,
  onPasswordVisibilityToggle,
  onBack,
  onCancel,
  onGenerate,
}: PasswordStepProps) => {
  const { t } = useTranslation("profile");
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
            onChange={(event) => onPasswordChange(event.target.value)}
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
                        onClick={onPasswordVisibilityToggle}
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
            onChange={(event) => onConfirmPasswordChange(event.target.value)}
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
                void onGenerate();
              }
            }}
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onBack} disabled={pending}>
          {t("clientEncryption.setupDialog.back")}
        </Button>
        <Box sx={{ flex: 1 }} />
        <Button onClick={onCancel} disabled={pending}>
          {t("clientEncryption.setupDialog.cancel")}
        </Button>
        <Button
          variant="contained"
          disabled={!canGenerate}
          onClick={onGenerate}
        >
          <PendingActionLabel
            pending={pending}
            pendingLabel={t("clientEncryption.setupDialog.generating")}
            label={t("clientEncryption.setupDialog.next")}
          />
        </Button>
      </DialogActions>
    </>
  );
};

type PhraseStepProps = {
  phrase: string | null;
  phraseWords: string[];
  phraseCopied: boolean;
  phraseStored: boolean;
  pending: boolean;
  error: string | null;
  onPhraseStoredChange: (stored: boolean) => void;
  onCancel: () => void;
  onCopyPhrase: () => void;
  onDownloadPhrase: () => void;
  onFinish: () => void;
};

export const PhraseStep = ({
  phrase,
  phraseWords,
  phraseCopied,
  phraseStored,
  pending,
  error,
  onPhraseStoredChange,
  onCancel,
  onCopyPhrase,
  onDownloadPhrase,
  onFinish,
}: PhraseStepProps) => {
  const { t } = useTranslation("profile");

  return (
    <>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity="warning">
            {t("clientEncryption.setupDialog.phraseWarning")}
          </Alert>
          <RecoveryPhraseGrid words={phraseWords} />
          <Stack direction="row" spacing={1} alignItems="center">
            <Button
              size="small"
              startIcon={<ContentCopyIcon fontSize="small" />}
              onClick={onCopyPhrase}
              disabled={!phrase}
            >
              {phraseCopied
                ? t("clientEncryption.setupDialog.copied")
                : t("clientEncryption.setupDialog.copy")}
            </Button>
            <Button
              size="small"
              startIcon={<DownloadIcon fontSize="small" />}
              onClick={onDownloadPhrase}
              disabled={!phrase}
            >
              {t("clientEncryption.setupDialog.download")}
            </Button>
          </Stack>
          <FormControlLabel
            control={
              <Checkbox
                checked={phraseStored}
                onChange={(_, checked) => onPhraseStoredChange(checked)}
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
          onClick={onFinish}
        >
          <PendingActionLabel
            pending={pending}
            pendingLabel={t("clientEncryption.setupDialog.saving")}
            label={t("clientEncryption.setupDialog.finish")}
          />
        </Button>
      </DialogActions>
    </>
  );
};

type RecoveryPhraseGridProps = {
  words: string[];
};

const RecoveryPhraseGrid = ({ words }: RecoveryPhraseGridProps) => (
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
    {words.map((word, index) => (
      <Box
        key={word + "-" + index}
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
);

type PendingActionLabelProps = {
  pending: boolean;
  pendingLabel: string;
  label: string;
};

const PendingActionLabel = ({
  pending,
  pendingLabel,
  label,
}: PendingActionLabelProps) => {
  if (!pending) {
    return label;
  }

  return (
    <Stack direction="row" spacing={1} alignItems="center">
      <CircularProgress size={16} />
      <span>{pendingLabel}</span>
    </Stack>
  );
};
