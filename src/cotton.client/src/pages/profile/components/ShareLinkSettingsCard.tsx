import { Link as LinkIcon } from "@mui/icons-material";
import {
  Box,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { usePreferencesStore } from "../../../shared/store/preferencesStore";
import { ProfileAccordionCard } from "./ProfileAccordionCard";

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

  const expireAfterMinutes = usePreferencesStore(
    (s) => s.shareLinkPreferences.expireAfterMinutes,
  );
  const setExpireAfterMinutes = usePreferencesStore(
    (s) => s.setShareLinkExpireAfterMinutes,
  );

  const selectedPreset = useMemo(
    () => findPreset(expireAfterMinutes),
    [expireAfterMinutes],
  );

  const currentDays = Math.max(
    1,
    Math.round(expireAfterMinutes / MINUTES_IN_DAY),
  );

  return (
    <ProfileAccordionCard
      id="share-links-header"
      ariaControls="share-links-content"
      icon={<LinkIcon color="primary" />}
      title={t("shareLinks.title")}
      description={t("shareLinks.description")}
    >
      <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
        <Typography variant="body2" color="text.secondary">
          {t("shareLinks.current", { days: currentDays })}
        </Typography>

        <ToggleButtonGroup
          exclusive
          size="small"
          value={selectedPreset}
          onChange={(_, value: PresetKey | null) => {
            if (!value) return;
            const preset = presets.find((p) => p.key === value);
            if (!preset) return;
            setExpireAfterMinutes(preset.minutes);
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
      </Box>
    </ProfileAccordionCard>
  );
};
