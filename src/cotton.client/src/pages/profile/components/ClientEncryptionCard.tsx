import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogTitle,
  FormControlLabel,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import LockIcon from "@mui/icons-material/Lock";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { useMemo, useState } from "react";
import type { ChangeEvent, ReactElement } from "react";
import { useTranslation } from "react-i18next";
import type { User } from "../../../features/auth/types";
import {
  hasEnvelopePreference,
  clearVaultSession,
  persistCurrentVaultSession,
  readEnvelopeFromPreferences,
  useVault,
} from "../../../shared/crypto";
import {
  selectClientEncryptionLockOnRefresh,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";
import { ClientEncryptionSetupForm } from "./ClientEncryptionSetupForm";
import { ClientEncryptionUnlockForm } from "./ClientEncryptionUnlockForm";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import { blurredDialogBackdropSlotProps } from "../../../shared/ui/dialogBackdrop";

type ClientEncryptionCardProps = {
  user: User;
  onUserUpdate: (user: User) => void;
};

type EncryptionStatus = "notSetUp" | "locked" | "unlocked" | "invalid";

export const ClientEncryptionCard = ({
  user,
  onUserUpdate,
}: ClientEncryptionCardProps) => {
  const { t } = useTranslation("profile");
  const isUnlocked = useVault((state) => state.isUnlocked);
  const lockVault = useVault((state) => state.lock);
  const storePreferences = useUserPreferencesStore((state) => state.preferences);
  const preferencesLoaded = useUserPreferencesStore((state) => state.loaded);
  const hydratePreferences = useUserPreferencesStore(
    (state) => state.hydrateFromRemote,
  );
  const lockOnRefresh = useUserPreferencesStore(
    selectClientEncryptionLockOnRefresh,
  );
  const setLockOnRefresh = useUserPreferencesStore(
    (state) => state.setClientEncryptionLockOnRefresh,
  );

  const preferences = useMemo(
    () => (preferencesLoaded ? storePreferences : (user.preferences ?? {})),
    [preferencesLoaded, storePreferences, user.preferences],
  );
  const hasEnvelope = hasEnvelopePreference(preferences);
  const envelope = useMemo(
    () => readEnvelopeFromPreferences(preferences),
    [preferences],
  );
  const [setupOpen, setSetupOpen] = useState(false);
  const [unlockOpen, setUnlockOpen] = useState(false);
  const handleLockOnRefreshChange = (
    event: ChangeEvent<HTMLInputElement>,
  ) => {
    const enabled = event.target.checked;
    setLockOnRefresh(enabled);

    if (enabled) {
      clearVaultSession();
      return;
    }

    void persistCurrentVaultSession();
  };

  const status: EncryptionStatus = !hasEnvelope
    ? "notSetUp"
    : envelope
      ? isUnlocked
        ? "unlocked"
        : "locked"
      : "invalid";

  const statusChip = status === "notSetUp" ? null : getStatusChip(status, t);
  const statusHint = status === "unlocked"
    ? t("clientEncryption.unlockedHint")
    : status === "locked"
      ? t("clientEncryption.lockedHint")
      : null;

  return (
    <>
      <ProfileAccordionCard
        id="client-encryption-header"
        ariaControls="client-encryption-content"
        icon={<LockOutlinedIcon color="primary" />}
        title={t("clientEncryption.sectionTitle")}
        description={t("clientEncryption.description")}
      >
        <Stack spacing={2} paddingY={2}>
          {status === "invalid" && (
            <Alert severity="error">
              {t("clientEncryption.invalidEnvelope")}
            </Alert>
          )}

          {status === "notSetUp" ? (
            <Box>
              <Button
                fullWidth
                variant="contained"
                onClick={() => setSetupOpen(true)}
              >
                {t("clientEncryption.actions.setup")}
              </Button>
            </Box>
          ) : status !== "invalid" ? (
            <Stack spacing={2}>
              <Stack
                direction={{ xs: "column", sm: "row" }}
                spacing={2}
                alignItems={{ xs: "stretch", sm: "center" }}
                justifyContent="space-between"
                sx={{
                  p: 2,
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 1,
                  bgcolor: "background.default",
                }}
              >
                <Stack spacing={1} minWidth={0}>
                  {statusChip}
                  {statusHint && (
                    <Typography variant="body2" color="text.secondary">
                      {statusHint}
                    </Typography>
                  )}
                </Stack>
                <Box sx={{ flexShrink: 0 }}>
                  {status === "locked" && envelope && (
                    <Button
                      fullWidth
                      variant="contained"
                      onClick={() => setUnlockOpen(true)}
                      sx={{ minWidth: 128 }}
                    >
                      {t("clientEncryption.actions.unlock")}
                    </Button>
                  )}
                  {status === "unlocked" && (
                    <Button
                      fullWidth
                      variant="outlined"
                      onClick={lockVault}
                      sx={{ minWidth: 128 }}
                    >
                      {t("clientEncryption.actions.lock")}
                    </Button>
                  )}
                </Box>
              </Stack>
              <FormControlLabel
                control={
                  <Switch
                    checked={lockOnRefresh}
                    onChange={handleLockOnRefreshChange}
                  />
                }
                label={t("clientEncryption.actions.lockOnRefresh")}
              />
            </Stack>
          ) : null}
        </Stack>
      </ProfileAccordionCard>

      <Dialog
        open={setupOpen}
        onClose={() => setSetupOpen(false)}
        fullWidth
        maxWidth="sm"
        slotProps={blurredDialogBackdropSlotProps}
      >
        <DialogTitle>{t("clientEncryption.setupDialog.title")}</DialogTitle>
        <ClientEncryptionSetupForm
          onCancel={() => setSetupOpen(false)}
          onSuccess={(preferences) => {
            hydratePreferences(preferences);
            onUserUpdate({ ...user, preferences });
            setSetupOpen(false);
          }}
        />
      </Dialog>

      <Dialog
        open={unlockOpen && Boolean(envelope)}
        onClose={() => setUnlockOpen(false)}
        fullWidth
        maxWidth="sm"
        slotProps={blurredDialogBackdropSlotProps}
      >
        <DialogTitle>{t("clientEncryption.unlockDialog.title")}</DialogTitle>
        {envelope && (
          <ClientEncryptionUnlockForm
            envelope={envelope}
            onCancel={() => setUnlockOpen(false)}
            onSuccess={() => setUnlockOpen(false)}
          />
        )}
      </Dialog>
    </>
  );
};

function getStatusChip(
  status: EncryptionStatus,
  t: (key: string) => string,
): ReactElement | null {
  if (status === "unlocked") {
    return (
      <Chip
        color="success"
        icon={<LockOpenIcon />}
        label={t("clientEncryption.status.unlocked")}
        size="small"
      />
    );
  }

  if (status === "locked") {
    return (
      <Chip
        color="warning"
        icon={<LockIcon />}
        label={t("clientEncryption.status.locked")}
        size="small"
      />
    );
  }

  if (status === "invalid") {
    return (
      <Chip
        color="error"
        icon={<WarningAmberIcon />}
        label={t("clientEncryption.status.invalid")}
        size="small"
      />
    );
  }

  return null;
}
