import { useCallback, useId, useMemo, useState, type ChangeEvent } from "react";
import { useTranslation } from "react-i18next";
import type { User } from "../../../../features/auth/types";
import { authApi } from "../../../../shared/api/authApi";
import {
  getApiErrorMessage,
  showApiErrorToast,
} from "../../../../shared/api/httpClient";
import { getCachedServerSettings } from "../../../../shared/api/queries/serverSettings";
import { useServerSettings } from "../../../../shared/store/useServerSettings";
import { uploadBlobToChunks } from "../../../../shared/upload";
import {
  formatDateOnly,
  getAgeYears,
  tryParseDateOnly,
} from "../../../../shared/utils/dateOnly";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import { toast } from "@shared/ui/notifications";
import {
  AvatarImageDecodeError,
  prepareAvatarForUpload,
} from "./avatarUploadUtils";
import { getAvatarInitials } from "./userInfoCardFormatters";

export type AvatarStatus = { kind: "idle" } | { kind: "error"; message: string };

interface UseUserInfoCardArgs {
  user: User;
  onUserUpdate: (updatedUser: User) => void;
}

interface UseUserInfoCardResult {
  avatarUploadInputId: string;
  avatarUploading: boolean;
  avatarStatus: AvatarStatus;
  emailVerificationSending: boolean;
  title: string;
  avatarInitials: string;
  birthDateValue: string;
  birthDateCompactValue: string;
  handleAvatarFileSelected: (event: ChangeEvent<HTMLInputElement>) => Promise<void>;
  handleSendEmailVerification: () => Promise<void>;
}

export const useUserInfoCard = ({
  user,
  onUserUpdate,
}: UseUserInfoCardArgs): UseUserInfoCardResult => {
  const { t } = useTranslation(["profile", "common"]);
  const avatarUploadInputId = useId();

  const [avatarUploading, setAvatarUploading] = useState(false);
  const [avatarStatus, setAvatarStatus] = useState<AvatarStatus>({
    kind: "idle",
  });
  const [emailVerificationSending, setEmailVerificationSending] =
    useState(false);

  const { data: serverSettings, fetchSettings: fetchServerSettings } =
    useServerSettings();

  const fullName = useMemo(
    () =>
      [user.firstName, user.lastName]
        .filter(
          (part): part is string =>
            typeof part === "string" && part.trim().length > 0,
        )
        .join(" "),
    [user.firstName, user.lastName],
  );

  const title = fullName || user.username;

  const avatarInitials = useMemo(
    () =>
      getAvatarInitials({
        firstName: user.firstName,
        lastName: user.lastName,
        username: user.username,
        email: user.email,
      }),
    [user.email, user.firstName, user.lastName, user.username],
  );

  const birthDateValues = useMemo(() => {
    if (!user.birthDate || user.birthDate.trim().length === 0) {
      // No birth date set: return empty so the row is hidden entirely.
      return {
        compact: "",
        full: "",
      };
    }

    const formatted = formatDateOnly(user.birthDate);
    const parsed = tryParseDateOnly(user.birthDate);
    if (!parsed) {
      return {
        compact: formatted,
        full: formatted,
      };
    }

    const ageYears = getAgeYears(parsed);
    if (ageYears < 0 || ageYears > 150) {
      return {
        compact: formatted,
        full: formatted,
      };
    }

    return {
      compact: formatted,
      full: `${formatted} (${t("ageYears", { count: ageYears })})`,
    };
  }, [t, user.birthDate]);

  const handleAvatarFileSelected = useCallback(
    async (event: ChangeEvent<HTMLInputElement>): Promise<void> => {
      const selectedFile = event.target.files?.[0];
      event.target.value = "";

      if (!selectedFile || avatarUploading) {
        return;
      }

      setAvatarStatus({ kind: "idle" });
      setAvatarUploading(true);

      try {
        let effectiveServerSettings = serverSettings;
        if (!effectiveServerSettings) {
          await fetchServerSettings({ force: false });
          effectiveServerSettings = getCachedServerSettings() ?? null;
        }

        if (!effectiveServerSettings) {
          setAvatarStatus({
            kind: "error",
            message: t("avatar.errors.settingsNotLoaded"),
          });
          return;
        }

        const preparedAvatar = await prepareAvatarForUpload(
          selectedFile,
          effectiveServerSettings.maxChunkSizeBytes,
        );

        if (!preparedAvatar) {
          setAvatarStatus({
            kind: "error",
            message: t("avatar.errors.fileTooLarge", {
              maxSize: formatBytes(effectiveServerSettings.maxChunkSizeBytes),
            }),
          });
          return;
        }

        const { chunkHashes } = await uploadBlobToChunks({
          blob: preparedAvatar.blob,
          fileName: preparedAvatar.fileName,
          server: {
            maxChunkSizeBytes: effectiveServerSettings.maxChunkSizeBytes,
            supportedHashAlgorithm:
              effectiveServerSettings.supportedHashAlgorithm,
          },
        });

        if (chunkHashes.length !== 1) {
          setAvatarStatus({
            kind: "error",
            message: t("avatar.errors.fileTooLarge", {
              maxSize: formatBytes(effectiveServerSettings.maxChunkSizeBytes),
            }),
          });
          return;
        }

        const updatedUser = await authApi.updateProfile({
          avatarHash: chunkHashes[0],
          username: user.username,
          email: user.email ?? null,
          firstName: user.firstName ?? null,
          lastName: user.lastName ?? null,
          birthDate: user.birthDate ?? null,
        });

        onUserUpdate(updatedUser);
      } catch (error) {
        if (error instanceof AvatarImageDecodeError) {
          setAvatarStatus({
            kind: "error",
            message: t("avatar.errors.unsupportedFormat"),
          });
          return;
        }

        const message = getApiErrorMessage(error);
        if (message) {
          setAvatarStatus({ kind: "error", message });
          return;
        }

        setAvatarStatus({ kind: "error", message: t("avatar.errors.failed") });
      } finally {
        setAvatarUploading(false);
      }
    },
    [
      avatarUploading,
      fetchServerSettings,
      onUserUpdate,
      serverSettings,
      t,
      user.birthDate,
      user.email,
      user.firstName,
      user.lastName,
      user.username,
    ],
  );

  const handleSendEmailVerification = useCallback(async (): Promise<void> => {
    if (emailVerificationSending || !user.email || user.isEmailVerified) {
      return;
    }

    setEmailVerificationSending(true);
    try {
      await authApi.sendEmailVerification();
      toast.success(t("emailVerification.sent"), {
        toastId: "profile:email-verification:sent",
      });
    } catch (error) {
      showApiErrorToast(
        error,
        t("emailVerification.errors.failed"),
        "profile:email-verification:error:generic",
      );
    } finally {
      setEmailVerificationSending(false);
    }
  }, [emailVerificationSending, t, user.email, user.isEmailVerified]);

  return {
    avatarUploadInputId,
    avatarUploading,
    avatarStatus,
    emailVerificationSending,
    title,
    avatarInitials,
    birthDateValue: birthDateValues.full,
    birthDateCompactValue: birthDateValues.compact,
    handleAvatarFileSelected,
    handleSendEmailVerification,
  };
};
