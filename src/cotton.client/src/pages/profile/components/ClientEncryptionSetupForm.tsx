import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import type { UserPreferences } from "../../../shared/api/userPreferencesApi";
import {
  persistEnvelope,
  setupEnvelope,
  useVault,
} from "../../../shared/crypto";
import {
  selectClientEncryptionLockOnRefresh,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";

import {
  MIN_PASSWORD_LENGTH,
  WarningStep,
  PasswordStep,
  PhraseStep,
} from "./ClientEncryptionSetupSteps";

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
  const lockOnRefresh = useUserPreferencesStore(
    selectClientEncryptionLockOnRefresh,
  );

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
      const blob = new Blob([phrase + "\n"], {
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
      unlockVault(masterKey, { persistToSession: !lockOnRefresh });
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

  switch (step) {
    case "warning":
      return (
        <WarningStep
          acknowledged={acknowledged}
          onAcknowledgeChange={setAcknowledged}
          onCancel={onCancel}
          onContinue={() => setStep("password")}
        />
      );
    case "password":
      return (
        <PasswordStep
          password={password}
          confirmPassword={confirmPassword}
          passwordVisible={passwordVisible}
          passwordTooShort={passwordTooShort}
          passwordMismatch={passwordMismatch}
          canGenerate={canGenerate}
          pending={pending}
          error={error}
          onPasswordChange={setPassword}
          onConfirmPasswordChange={setConfirmPassword}
          onPasswordVisibilityToggle={() =>
            setPasswordVisible((value) => !value)
          }
          onBack={() => setStep("warning")}
          onCancel={onCancel}
          onGenerate={handleGenerate}
        />
      );
    case "phrase":
      return (
        <PhraseStep
          phrase={phrase}
          phraseWords={phraseWords}
          phraseCopied={phraseCopied}
          phraseStored={phraseStored}
          pending={pending}
          error={error}
          onPhraseStoredChange={setPhraseStored}
          onCancel={onCancel}
          onCopyPhrase={handleCopyPhrase}
          onDownloadPhrase={handleDownloadPhrase}
          onFinish={handleFinish}
        />
      );
  }
};
