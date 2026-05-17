import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import LockIcon from "@mui/icons-material/Lock";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import NoEncryptionOutlinedIcon from "@mui/icons-material/NoEncryptionOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { useMemo, useState } from "react";
import type { ReactElement } from "react";
import { useTranslation } from "react-i18next";
import type { User } from "../../../features/auth/types";
import {
  hasEnvelopePreference,
  readEnvelopeFromPreferences,
  useVault,
} from "../../../shared/crypto";
import { useUserPreferencesStore } from "../../../shared/store/userPreferencesStore";
import { ClientEncryptionSetupForm } from "./ClientEncryptionSetupForm";
import { ClientEncryptionUnlockForm } from "./ClientEncryptionUnlockForm";
import { ProfileAccordionCard } from "./ProfileAccordionCard";

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

  const status: EncryptionStatus = !hasEnvelope
    ? "notSetUp"
    : envelope
      ? isUnlocked
        ? "unlocked"
        : "locked"
      : "invalid";

  const statusChip = getStatusChip(status, t);

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
          <Stack direction="row" spacing={1} alignItems="center">
            {statusChip}
          </Stack>

          {status === "invalid" && (
            <Alert severity="error">
              {t("clientEncryption.invalidEnvelope")}
            </Alert>
          )}

          {status === "unlocked" && (
            <Typography variant="body2" color="text.secondary">
              {t("clientEncryption.unlockedHint")}
            </Typography>
          )}

          <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
            {status === "notSetUp" && (
              <Button variant="contained" onClick={() => setSetupOpen(true)}>
                {t("clientEncryption.actions.setup")}
              </Button>
            )}
            {status === "locked" && envelope && (
              <Button variant="contained" onClick={() => setUnlockOpen(true)}>
                {t("clientEncryption.actions.unlock")}
              </Button>
            )}
            {status === "unlocked" && (
              <Button variant="outlined" onClick={lockVault}>
                {t("clientEncryption.actions.lock")}
              </Button>
            )}
          </Stack>
        </Stack>
      </ProfileAccordionCard>

      <Dialog
        open={setupOpen}
        onClose={() => setSetupOpen(false)}
        fullWidth
        maxWidth="sm"
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
): ReactElement {
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

  return (
    <Chip
      icon={<NoEncryptionOutlinedIcon />}
      label={t("clientEncryption.status.notSetUp")}
      size="small"
    />
  );
}
