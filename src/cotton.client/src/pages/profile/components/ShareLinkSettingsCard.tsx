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
  useUserPreferencesStore,
  USER_PREFERENCE_KEYS,
} from "../../../shared/store/userPreferencesStore";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import { authApi } from "../../../shared/api/authApi";

const MINUTES_IN_DAY = 60 * 24;

type PresetKey = "oneDay" | "week" | "month" | "threeMonths" | "year";

type Preset = {
  key: PresetKey;
  minutes: number;
};

const presets: Preset[] = [
  { key: "oneDay", minutes: MINUTES_IN_DAY },
  { key: "week", minutes: MINUTES_IN_DAY * 7 },
  { key: "month", minutes: MINUTES_IN_DAY * 30 },
  { key: "threeMonths", minutes: MINUTES_IN_DAY * 90 },
  { key: "year", minutes: MINUTES_IN_DAY * 365 },
];

const findPreset = (minutes: number): PresetKey | null => {
  const found = presets.find((p) => p.minutes === minutes);
  return found ? found.key : null;
};

export const ShareLinkSettingsCard = () => {
  const { t } = useTranslation("profile");
  const confirm = useConfirm();

  const expireAfterMinutes = useUserPreferencesStore(
    selectShareLinkExpireAfterMinutes,
  );
  const updatePreferences = useUserPreferencesStore((s) => s.updatePreferences);

  const [invalidating, setInvalidating] = useState(false);
  const [invalidated, setInvalidated] = useState(false);
  const [invalidateError, setInvalidateError] = useState("");

  const selectedPreset = useMemo(
    () => findPreset(expireAfterMinutes),
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
            onChange={(_, value: PresetKey | null) => {
              if (!value) return;
              const preset = presets.find((p) => p.key === value);
              if (!preset) return;
              void updatePreferences({
                [USER_PREFERENCE_KEYS.shareLinkExpireAfterMinutes]: `${preset.minutes}`,
              });
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
            <ToggleButton value="oneDay">
              {t("shareLinks.presets.oneDay")}
            </ToggleButton>
            <ToggleButton value="week">
              {t("shareLinks.presets.week")}
            </ToggleButton>
            <ToggleButton value="month">
              {t("shareLinks.presets.month")}
            </ToggleButton>
            <ToggleButton value="threeMonths">
              {t("shareLinks.presets.threeMonths")}
            </ToggleButton>
            <ToggleButton value="year">
              {t("shareLinks.presets.year")}
            </ToggleButton>
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
      </Box>
    </ProfileAccordionCard>
  );
};
