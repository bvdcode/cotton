import { useCallback, useId, useMemo, useState, type ChangeEvent } from "react";
import { useTranslation } from "react-i18next";
import type { User } from "../../../../features/auth/types";
import { authApi } from "../../../../shared/api/authApi";
import {
  hasApiErrorToastBeenDispatched,
  isAxiosError,
} from "../../../../shared/api/httpClient";
import { useSettingsStore } from "../../../../shared/store/settingsStore";
import { uploadBlobToChunks } from "../../../../shared/upload";
import {
  formatDateOnly,
  getAgeYears,
  tryParseDateOnly,
} from "../../../../shared/utils/dateOnly";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import { toast } from "react-toastify";
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

  const serverSettings = useSettingsStore((state) => state.data);
  const fetchServerSettings = useSettingsStore((state) => state.fetchSettings);

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

  const birthDateValue = useMemo(() => {
    const placeholder = t("common:placeholder");
    if (!user.birthDate || user.birthDate.trim().length === 0) {
      return placeholder;
    }

    const formatted = formatDateOnly(user.birthDate);
    const parsed = tryParseDateOnly(user.birthDate);
    if (!parsed) {
      return formatted;
    }

    const ageYears = getAgeYears(parsed);
    if (ageYears < 0 || ageYears > 150) {
      return formatted;
    }

    return `${formatted} (${t("ageYears", { count: ageYears })})`;
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
          effectiveServerSettings = useSettingsStore.getState().data;
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

        if (isAxiosError(error)) {
          const data = error.response?.data as
            | { message?: string; title?: string }
            | undefined;
          const message = data?.message ?? data?.title;
          if (message) {
            setAvatarStatus({ kind: "error", message });
            return;
          }
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
      if (isAxiosError(error) && hasApiErrorToastBeenDispatched(error)) {
        return;
      }

      toast.error(t("emailVerification.errors.failed"), {
        toastId: "profile:email-verification:error:generic",
      });
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
    birthDateValue,
    handleAvatarFileSelected,
    handleSendEmailVerification,
  };
};
