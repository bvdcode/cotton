import { Link as LinkIcon } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  ToggleButton,
  ToggleButtonGroup,
} from "@mui/material";
import { useCallback, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import {
  selectShareLinkExpireAfterMinutes,
  selectShareLinkNeverExpiresWarningDismissed,
  useUserPreferencesStore,
  USER_PREFERENCE_KEYS,
} from "../../../shared/store/userPreferencesStore";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import { authApi } from "../../../shared/api/authApi";
import { shareLinkExpirationPresets } from "../../../shared/utils/shareLinkExpiration";

export const ShareLinkSettingsCard = () => {
  const { t } = useTranslation("profile");
  const confirm = useConfirm();

  const expireAfterMinutes = useUserPreferencesStore(
    selectShareLinkExpireAfterMinutes,
  );
  const updatePreferences = useUserPreferencesStore((s) => s.updatePreferences);
  const setExpiration = useUserPreferencesStore(
    (s) => s.setShareLinkExpireAfterMinutes,
  );
  const warningDismissed = useUserPreferencesStore(
    selectShareLinkNeverExpiresWarningDismissed,
  );

  const [invalidating, setInvalidating] = useState(false);
  const [invalidated, setInvalidated] = useState(false);
  const [invalidateError, setInvalidateError] = useState("");

  const selectedPreset = useMemo(
    () =>
      shareLinkExpirationPresets.find((p) => p.minutes === expireAfterMinutes)
        ?.key ?? null,
    [expireAfterMinutes],
  );

  const handleInvalidateAll = useCallback(async () => {
    await confirm({
      title: t("shareLinks.invalidateConfirmTitle"),
      description: t("shareLinks.invalidateConfirmDescription"),
      confirmationText: t("shareLinks.invalidateAll"),
    }).then(async (result) => {
      if (result.confirmed) {
        setInvalidateError("");
        setInvalidated(false);
        setInvalidating(true);
        try {
          await authApi.invalidateShareLinks();
          setInvalidated(true);
        } catch {
          setInvalidateError(t("shareLinks.errors.invalidateFailed"));
        } finally {
          setInvalidating(false);
        }
      }
    });
  }, [confirm, t]);

  return (
    <ProfileAccordionCard
      id="share-links-header"
      ariaControls="share-links-content"
      icon={<LinkIcon color="primary" />}
      title={t("shareLinks.title")}
      description={t("shareLinks.description")}
      warning={
        expireAfterMinutes === null
          ? t("shareLinks.neverExpiresWarning")
          : undefined
      }
    >
      <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
        <Box
          sx={{
            display: "flex",
            flexDirection: { xs: "column", sm: "row" },
            gap: 1.5,
            justifyContent: "space-between",
            alignItems: { sm: "center" },
          }}
        >
          <ToggleButtonGroup
            exclusive
            size="small"
            value={selectedPreset}
            aria-label={t("shareLinks.description")}
            onChange={(_, value: string | null) => {
              if (!value) return;
              const preset = shareLinkExpirationPresets.find(
                (p) => p.key === value,
              );
              if (!preset) return;
              setExpiration(preset.minutes);
            }}
            sx={{
              width: { xs: "100%", sm: "auto" },
              flexWrap: "wrap",
              "& .MuiToggleButton-root": {
                flex: { xs: "1 1 auto", sm: "0 0 auto" },
                whiteSpace: "nowrap",
              },
            }}
          >
            {shareLinkExpirationPresets.map((preset) => (
              <ToggleButton key={preset.key} value={preset.key}>
                {t(`shareLinks.presets.${preset.key}`)}
              </ToggleButton>
            ))}
          </ToggleButtonGroup>

          <Button
            variant="outlined"
            color="error"
            onClick={handleInvalidateAll}
            disabled={invalidating}
            sx={{ width: { xs: "100%", sm: "auto" }, whiteSpace: "nowrap" }}
          >
            {invalidating
              ? t("shareLinks.invalidating")
              : t("shareLinks.invalidateAll")}
          </Button>
        </Box>

        {invalidated && (
          <Alert severity="success">{t("shareLinks.invalidated")}</Alert>
        )}
        {invalidateError && <Alert severity="error">{invalidateError}</Alert>}
        {expireAfterMinutes === null && !warningDismissed && (
          <Alert
            severity="warning"
            action={
              <Button
                color="inherit"
                size="small"
                onClick={() =>
                  void updatePreferences({
                    [USER_PREFERENCE_KEYS.shareLinkNeverExpiresWarningDismissed]:
                      "true",
                  })
                }
              >
                {t("shareLinks.dismissWarning")}
              </Button>
            }
          >
            {t("shareLinks.neverExpiresDescription")}
          </Alert>
        )}
      </Box>
    </ProfileAccordionCard>
  );
};
