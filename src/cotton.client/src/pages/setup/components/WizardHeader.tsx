import { Box, Stack, Typography, IconButton, Tooltip } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useTheme } from "../../../app/providers";
import {
  Brightness4 as DarkIcon,
  Brightness7 as LightIcon,
  Language as LanguageIcon,
} from "@mui/icons-material";

export function WizardHeader({ t }: { t: (key: string) => string }) {
  const { i18n } = useTranslation();
  const { mode, setTheme } = useTheme();

  const toggleLanguage = () => {
    const currentLang = i18n.language;
    const newLang = currentLang === "en" ? "ru" : "en";
    i18n.changeLanguage(newLang);
  };

  const toggleTheme = () => {
    setTheme(mode === "light" ? "dark" : "light");
  };

  return (
    <Stack spacing={1.5}>
      <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between" }}>
        <Stack spacing={0.5} sx={{ flex: 1 }}>
          <Typography variant="h4" fontWeight={800}>
            {t("title")}
          </Typography>
          <Typography variant="body1" color="text.secondary">
            {t("subtitle")}
          </Typography>
        </Stack>
        
        <Stack direction="row" spacing={0.5}>
          <Tooltip title={i18n.language === "en" ? "Switch to Russian" : "Переключить на английский"}>
            <IconButton onClick={toggleLanguage} size="medium">
              <LanguageIcon />
            </IconButton>
          </Tooltip>
          <Tooltip title={mode === "light" ? "Dark mode" : "Light mode"}>
            <IconButton onClick={toggleTheme} size="medium">
              {mode === "light" ? <DarkIcon /> : <LightIcon />}
            </IconButton>
          </Tooltip>
        </Stack>
      </Box>
    </Stack>
  );
}
