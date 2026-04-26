import {
  Box,
  Divider,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  ToggleButton,
  ToggleButtonGroup,
} from "@mui/material";
import {
  Brightness4,
  Brightness7,
  SettingsBrightness,
  PaletteOutlined,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import i18n from "../../../i18n";
import { ProfileAccordionCard } from "./ProfileAccordionCard";
import {
  selectUiLanguage,
  selectThemeMode,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";
import type { ThemeMode } from "../../../shared/theme";
import {
  selectGalleryPreferPreview,
  selectGallerySmoothTransitions,
  useLocalPreferencesStore,
} from "../../../shared/store/localPreferencesStore";
import { supportedLanguages, type SupportedLanguage } from "../../../locales";

export const AppearanceSettingsCard = () => {
  const { t } = useTranslation("profile");

  const themeMode = useUserPreferencesStore(selectThemeMode);
  const setThemeMode = useUserPreferencesStore((s) => s.setThemeMode);
  const setUiLanguage = useUserPreferencesStore((s) => s.setUiLanguage);
  const preferredLanguage = useUserPreferencesStore(selectUiLanguage);

  const smoothGalleryTransitions = useLocalPreferencesStore(
    selectGallerySmoothTransitions,
  );
  const setSmoothGalleryTransitions = useLocalPreferencesStore(
    (s) => s.setGallerySmoothTransitions,
  );
  const galleryPreferPreview = useLocalPreferencesStore(
    selectGalleryPreferPreview,
  );
  const setGalleryPreferPreview = useLocalPreferencesStore(
    (s) => s.setGalleryPreferPreview,
  );

  const selectedLanguage = (preferredLanguage ??
    i18n.language ??
    "en") as SupportedLanguage;

  return (
    <ProfileAccordionCard
      id="appearance-settings-header"
      ariaControls="appearance-settings-content"
      icon={<PaletteOutlined color="primary" />}
      title={t("appearance.title")}
      description={t("appearance.description")}
    >
      <Stack spacing={2} paddingY={2}>
        <Box>
          <ToggleButtonGroup
            fullWidth
            exclusive
            value={themeMode}
            onChange={(_, value: ThemeMode | null) => {
              if (!value) return;
              setThemeMode(value);
            }}
            aria-label={t("appearance.themeMode.label")}
          >
            <ToggleButton
              value="light"
              aria-label={t("appearance.themeMode.light")}
              sx={{ display: "flex", gap: 1 }}
            >
              <Brightness7 fontSize="small" />
              {t("appearance.themeMode.light")}
            </ToggleButton>
            <ToggleButton
              value="system"
              aria-label={t("appearance.themeMode.system")}
              sx={{ display: "flex", gap: 1 }}
            >
              <SettingsBrightness fontSize="small" />
              {t("appearance.themeMode.system")}
            </ToggleButton>
            <ToggleButton
              value="dark"
              aria-label={t("appearance.themeMode.dark")}
              sx={{ display: "flex", gap: 1 }}
            >
              <Brightness4 fontSize="small" />
              {t("appearance.themeMode.dark")}
            </ToggleButton>
          </ToggleButtonGroup>
        </Box>

        <FormControl fullWidth size="small">
          <InputLabel id="appearance-language-label">
            {t("appearance.language.label")}
          </InputLabel>
          <Select
            labelId="appearance-language-label"
            label={t("appearance.language.label")}
            value={selectedLanguage}
            onChange={(e) => setUiLanguage(e.target.value as SupportedLanguage)}
          >
            {supportedLanguages.map((lng) => (
              <MenuItem key={lng} value={lng}>
                {t(`appearance.language.${lng}`)}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <Divider />

        <FormControlLabel
          control={
            <Switch
              checked={smoothGalleryTransitions}
              onChange={(e) => setSmoothGalleryTransitions(e.target.checked)}
            />
          }
          label={t("appearance.gallerySmoothTransitions")}
        />

        <FormControlLabel
          control={
            <Switch
              checked={galleryPreferPreview}
              onChange={(e) => setGalleryPreferPreview(e.target.checked)}
            />
          }
          label={t("appearance.galleryPreferPreview")}
        />
      </Stack>
    </ProfileAccordionCard>
  );
};
