import {
  Box,
  Stack,
  Typography,
  IconButton,
  Tooltip,
  Avatar,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useTheme } from "../../../app/providers";
import { supportedLanguages } from "../../../locales";
import { useUserPreferencesStore } from "../../../shared/store/userPreferencesStore";
import {
  Brightness4 as DarkIcon,
  Brightness7 as LightIcon,
  Translate as TranslateIcon,
} from "@mui/icons-material";

export function WizardHeader() {
  const { i18n, t: tCommon } = useTranslation("common");
  const { t } = useTranslation("setup");
  const { mode, setTheme } = useTheme();
  const setUiLanguage = useUserPreferencesStore((s) => s.setUiLanguage);

  const toggleLanguage = () => {
    const currentLang = i18n.language;
    const currentIndex = supportedLanguages.indexOf(currentLang);
    const nextIndex = (currentIndex + 1) % supportedLanguages.length;
    setUiLanguage(supportedLanguages[nextIndex]);
  };

  const toggleTheme = () => {
    setTheme(mode === "light" ? "dark" : "light");
  };

  // Get next language for tooltip
  const currentLang = i18n.language;
  const currentIndex = supportedLanguages.indexOf(currentLang);
  const nextIndex = (currentIndex + 1) % supportedLanguages.length;
  const nextLanguage = supportedLanguages[nextIndex];
  const nextLanguageLabel = i18n.getFixedT(
    nextLanguage,
    "common",
  )("switchToThisLanguage");

  return (
    <Stack spacing={{ xs: 1, sm: 1.5 }}>
      <Box
        sx={{
          display: "flex",
          alignItems: { xs: "center", sm: "flex-start" },
          justifyContent: "space-between",
          gap: { xs: 1, sm: 2 },
        }}
      >
        <Stack spacing={{ xs: 0.5, sm: 0.5 }} sx={{ flex: 1, minWidth: 0 }}>
          <Box
            display="flex"
            gap={{ xs: 1.5, sm: 2 }}
            alignItems="center"
            sx={{ flexWrap: "nowrap" }}
          >
            <Avatar
              src="/assets/icons/icon.svg"
              alt="Cotton Logo"
              sx={{ width: { xs: 32, sm: 40 }, height: { xs: 32, sm: 40 } }}
            />
            <Typography
              variant="h4"
              fontWeight={800}
              sx={{
                fontSize: { xs: "1.25rem", sm: "1.5rem", md: "2rem" },
                lineHeight: 1.2,
              }}
            >
              {t("title")}
            </Typography>
          </Box>
          <Typography
            variant="body1"
            color="text.secondary"
            sx={{
              fontSize: { xs: "0.875rem", sm: "1rem" },
              display: { xs: "none", sm: "block" },
            }}
          >
            {t("subtitle")}
          </Typography>
        </Stack>

        <Stack
          direction="row"
          spacing={{ xs: 0, sm: 0.5 }}
          sx={{ flexShrink: 0 }}
        >
          <Tooltip title={nextLanguageLabel}>
            <IconButton onClick={toggleLanguage} size="small">
              <TranslateIcon sx={{ fontSize: { xs: 20, sm: 24 } }} />
            </IconButton>
          </Tooltip>
          <Tooltip
            title={
              mode === "light"
                ? tCommon("userMenu.darkMode")
                : tCommon("userMenu.lightMode")
            }
          >
            <IconButton onClick={toggleTheme} size="small">
              {mode === "light" ? (
                <DarkIcon sx={{ fontSize: { xs: 20, sm: 24 } }} />
              ) : (
                <LightIcon sx={{ fontSize: { xs: 20, sm: 24 } }} />
              )}
            </IconButton>
          </Tooltip>
        </Stack>
      </Box>
    </Stack>
  );
}
