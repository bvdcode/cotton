import { useTranslation } from "react-i18next";
import { useState } from "react";
import {
  getApiErrorMessage,
  isAxiosError,
} from "../../../shared/api/httpClient";
import { totpApi, type TotpSetup } from "../../../shared/api/totpApi";
import { authApi } from "../../../shared/api/authApi";
import type { User } from "../../../features/auth/types";
import {
  TotpStatusIcon,
  EnabledTotpContent,
  DisabledTotpContent,
  DisableTotpDialog,
} from "./TotpSettingsContent";
import { ProfileAccordionCard } from "./ProfileAccordionCard";

interface TotpSettingsCardProps {
  user: User;
  onUserUpdate: (user: User) => void;
}

export const TotpSettingsCard = ({
  user,
  onUserUpdate,
}: TotpSettingsCardProps) => {
  const { t } = useTranslation("profile");

  const [totpSetup, setTotpSetup] = useState<TotpSetup | null>(null);
  const [totpLoading, setTotpLoading] = useState(false);
  const [totpConfirmLoading, setTotpConfirmLoading] = useState(false);
  const [totpCode, setTotpCode] = useState("");
  const [totpError, setTotpError] = useState<string | null>(null);
  const [totpSuccess, setTotpSuccess] = useState(false);

  const [disableDialogOpen, setDisableDialogOpen] = useState(false);
  const [disablePassword, setDisablePassword] = useState("");
  const [disablePasswordVisible, setDisablePasswordVisible] = useState(false);
  const [disableLoading, setDisableLoading] = useState(false);
  const [disableError, setDisableError] = useState<string | null>(null);

  const totpEnabled = Boolean(user.isTotpEnabled);
  const description = totpEnabled
    ? t("totp.enabledMessage")
    : t("totp.setup.caption");

  const handleSetupTotp = async () => {
    setTotpError(null);
    setTotpSuccess(false);
    setTotpLoading(true);
    try {
      const setup = await totpApi.setup();
      setTotpSetup(setup);
    } catch (e) {
      setTotpError(resolveTotpSetupError(e, t));
    } finally {
      setTotpLoading(false);
    }
  };

  const handleConfirmTotp = async () => {
    setTotpError(null);
    setTotpSuccess(false);
    const normalizedTotpCode = totpCode.replace(/\D/g, "").slice(0, 6);
    if (normalizedTotpCode.length < 6) {
      setTotpError(t("totp.errors.codeRequired"));
      return;
    }

    setTotpConfirmLoading(true);
    try {
      await totpApi.confirm(normalizedTotpCode);
      const refreshed = await authApi.me();
      onUserUpdate(refreshed);
      setTotpSuccess(true);
      setTotpSetup(null);
      setTotpCode("");
    } catch (e) {
      setTotpError(resolveTotpConfirmError(e, t));
    } finally {
      setTotpConfirmLoading(false);
    }
  };

  const handleCopySecret = async () => {
    if (!totpSetup?.secretBase32) {
      return;
    }

    try {
      await navigator.clipboard.writeText(totpSetup.secretBase32);
    } catch {
      // ignore clipboard errors
    }
  };

  const handleOpenDisableDialog = () => {
    setDisablePassword("");
    setDisableError(null);
    setDisablePasswordVisible(false);
    setDisableDialogOpen(true);
  };

  const handleDisableTotp = async () => {
    setDisableError(null);
    if (!disablePassword) {
      setDisableError(t("totp.errors.invalidPassword"));
      return;
    }

    setDisableLoading(true);
    try {
      await totpApi.disable(disablePassword);
      const refreshed = await authApi.me();
      onUserUpdate(refreshed);
      setDisableDialogOpen(false);
      setTotpSuccess(false);
      setTotpError(null);
    } catch (e) {
      setDisableError(resolveTotpDisableError(e, t));
    } finally {
      setDisableLoading(false);
    }
  };

  return (
    <>
      <ProfileAccordionCard
        id="totp-settings-header"
        ariaControls="totp-settings-content"
        icon={<TotpStatusIcon enabled={totpEnabled} />}
        title={t("totp.sectionTitle")}
        description={description}
      >
        {totpEnabled ? (
          <EnabledTotpContent
            enabledAt={user.totpEnabledAt ?? null}
            failedAttempts={user.totpFailedAttempts ?? 0}
            onDisable={handleOpenDisableDialog}
          />
        ) : (
          <DisabledTotpContent
            totpSetup={totpSetup}
            totpCode={totpCode}
            totpLoading={totpLoading}
            totpConfirmLoading={totpConfirmLoading}
            totpError={totpError}
            totpSuccess={totpSuccess}
            onSetup={handleSetupTotp}
            onTotpCodeChange={setTotpCode}
            onConfirm={handleConfirmTotp}
            onCopySecret={handleCopySecret}
          />
        )}
      </ProfileAccordionCard>
      <DisableTotpDialog
        open={disableDialogOpen}
        password={disablePassword}
        passwordVisible={disablePasswordVisible}
        loading={disableLoading}
        error={disableError}
        onClose={() => setDisableDialogOpen(false)}
        onPasswordChange={setDisablePassword}
        onPasswordVisibilityToggle={() => setDisablePasswordVisible((v) => !v)}
        onConfirm={handleDisableTotp}
      />
    </>
  );
};

type Translate = ReturnType<typeof useTranslation>["t"];

const resolveTotpSetupError = (error: unknown, t: Translate) => {
  if (isAxiosError(error) && error.response?.status === 409) {
    return t("totp.errors.alreadyEnabled");
  }

  return getApiErrorMessage(error) ?? t("totp.errors.setupFailed");
};

const resolveTotpConfirmError = (error: unknown, t: Translate) => {
  if (isAxiosError(error)) {
    const statusMessage = getConfirmStatusMessage(error.response?.status, t);
    if (statusMessage) {
      return statusMessage;
    }
  }

  return getApiErrorMessage(error) ?? t("totp.errors.confirmFailed");
};

const getConfirmStatusMessage = (status: number | undefined, t: Translate) => {
  switch (status) {
    case 403:
      return t("totp.errors.invalidCode");
    case 400:
      return t("totp.errors.setupNotInitiated");
    case 409:
      return t("totp.errors.alreadyEnabled");
    default:
      return null;
  }
};

const resolveTotpDisableError = (error: unknown, t: Translate) => {
  if (isAxiosError(error) && error.response?.status === 403) {
    return t("totp.errors.invalidPassword");
  }

  return getApiErrorMessage(error) ?? t("totp.errors.disableFailed");
};
