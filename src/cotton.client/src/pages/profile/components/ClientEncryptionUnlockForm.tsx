import {
  Alert,
  Button,
  CircularProgress,
  DialogActions,
  DialogContent,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Tooltip,
} from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import VisibilityOffIcon from "@mui/icons-material/VisibilityOff";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  CorruptedContainerError,
  InvalidRecoveryPhraseError,
  UnsupportedVersionError,
  unlockWithPassword,
  unlockWithRecovery,
  useVault,
  WrongUnlockError,
} from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";

type UnlockMode = "password" | "phrase";

type ClientEncryptionUnlockFormProps = {
  envelope: Uint8Array;
  onCancel: () => void;
  onSuccess: () => void;
};

export const ClientEncryptionUnlockForm = ({
  envelope,
  onCancel,
  onSuccess,
}: ClientEncryptionUnlockFormProps) => {
  const { t } = useTranslation("profile");
  const unlockVault = useVault((state) => state.unlock);

  const [mode, setMode] = useState<UnlockMode>("password");
  const [password, setPassword] = useState("");
  const [phrase, setPhrase] = useState("");
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const phraseWordCount = phrase.trim().split(/\s+/).filter(Boolean).length;
  const canSubmit =
    !pending && (mode === "password" ? password.length > 0 : phraseWordCount === 24);

  const handleSubmit = async () => {
    setError(null);
    setPending(true);

    try {
      const masterKey =
        mode === "password"
          ? await unlockWithPassword(envelope, password)
          : await unlockWithRecovery(envelope, phrase);
      unlockVault(masterKey);
      void useNodesStore.getState().refreshCachedFileDisplayMetadata();
      onSuccess();
    } catch (error) {
      if (error instanceof WrongUnlockError) {
        setError(
          mode === "password"
            ? t("clientEncryption.unlockDialog.errors.wrongPassword")
            : t("clientEncryption.unlockDialog.errors.wrongPhrase"),
        );
      } else if (error instanceof InvalidRecoveryPhraseError) {
        setError(t("clientEncryption.unlockDialog.errors.invalidPhrase"));
      } else if (
        error instanceof CorruptedContainerError ||
        error instanceof UnsupportedVersionError
      ) {
        setError(t("clientEncryption.unlockDialog.errors.invalidEnvelope"));
      } else {
        setError(t("clientEncryption.unlockDialog.errors.unknown"));
      }
    } finally {
      setPending(false);
    }
  };

  const visibilityLabel = passwordVisible ? t("password.hide") : t("password.show");

  return (
    <>
      <DialogContent dividers>
        <Stack spacing={2} pt={0.5}>
          {mode === "password" ? (
            <TextField
              label={t("clientEncryption.unlockDialog.passwordLabel")}
              type={passwordVisible ? "text" : "password"}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              fullWidth
              disabled={pending}
              autoFocus
              onKeyDown={(event) => {
                if (event.key === "Enter" && canSubmit) {
                  void handleSubmit();
                }
              }}
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
          ) : (
            <TextField
              label={t("clientEncryption.unlockDialog.phraseLabel")}
              value={phrase}
              onChange={(event) => setPhrase(event.target.value)}
              multiline
              minRows={3}
              autoComplete="off"
              spellCheck={false}
              fullWidth
              disabled={pending}
              autoFocus
            />
          )}
          {error && <Alert severity="error">{error}</Alert>}
          <Button
            variant="text"
            size="small"
            sx={{ alignSelf: "flex-start", textTransform: "none" }}
            disabled={pending}
            onClick={() => {
              setMode((value) => (value === "password" ? "phrase" : "password"));
              setError(null);
            }}
          >
            {mode === "password"
              ? t("clientEncryption.unlockDialog.usePhrase")
              : t("clientEncryption.unlockDialog.usePassword")}
          </Button>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={pending}>
          {t("clientEncryption.unlockDialog.cancel")}
        </Button>
        <Button variant="contained" disabled={!canSubmit} onClick={handleSubmit}>
          {pending ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <CircularProgress size={16} />
              <span>{t("clientEncryption.unlockDialog.unlocking")}</span>
            </Stack>
          ) : (
            t("clientEncryption.unlockDialog.submit")
          )}
        </Button>
      </DialogActions>
    </>
  );
};
