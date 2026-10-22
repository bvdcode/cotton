import { useEffect, useRef } from "react";
import { useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useVault } from "@shared/crypto/vault";
import { uploadSharedFiles } from "@shared/pwa/sharedFiles";
import {
  SHARE_TARGET_ERROR,
  SHARE_TARGET_QUERY,
} from "@shared/pwa/shareTargetConfig";
import { toast } from "@shared/ui/notifications";
import { reportClientError } from "@shared/utils/clientDiagnostics";

interface SharedFileUploadOptions {
  ready: boolean;
  encryptionRequired: boolean;
  upload: (files: File[]) => Promise<void>;
}

export const useSharedFileUpload = ({
  ready,
  encryptionRequired,
  upload,
}: SharedFileUploadOptions): void => {
  const [searchParams, setSearchParams] = useSearchParams();
  const shareId = searchParams.get(SHARE_TARGET_QUERY);
  const isVaultUnlocked = useVault((state) => state.isUnlocked);
  const { t } = useTranslation("files");
  const pending = useRef<{ id: string; promise: Promise<boolean> } | null>(
    null,
  );

  useEffect(() => {
    if (!shareId || !ready || (encryptionRequired && !isVaultUnlocked)) {
      return;
    }

    let cancelled = false;
    if (pending.current?.id !== shareId) {
      pending.current = {
        id: shareId,
        promise:
          shareId === SHARE_TARGET_ERROR
            ? Promise.resolve(false)
            : uploadSharedFiles(shareId, upload),
      };
    }

    void pending.current.promise
      .then((received) => {
        if (cancelled) {
          return;
        }
        if (!received) {
          toast.error(t("shareTarget.receiveFailed"));
        }
        setSearchParams(
          (current) => {
            const next = new URLSearchParams(current);
            next.delete(SHARE_TARGET_QUERY);
            return next;
          },
          { replace: true },
        );
      })
      .catch((error: Error) => {
        reportClientError("Failed to upload shared files", error);
        if (!cancelled) {
          toast.error(t("shareTarget.uploadFailed"));
        }
      });

    return () => {
      cancelled = true;
    };
  }, [
    shareId,
    ready,
    encryptionRequired,
    isVaultUnlocked,
    upload,
    setSearchParams,
    t,
  ]);
};
