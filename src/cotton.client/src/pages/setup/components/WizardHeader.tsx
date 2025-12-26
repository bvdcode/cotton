import { Box, Stack, Typography, IconButton, Tooltip } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useTheme } from "../../../app/providers";
import { supportedLanguages } from "../../../locales";
import {
  Brightness4 as DarkIcon,
  Brightness7 as LightIcon,
  Translate as TranslateIcon
} from "@mui/icons-material";

export function WizardHeader({ t }: { t: (key: string) => string }) {
  const { i18n, t: tCommon } = useTranslation("common");
  const { mode, setTheme } = useTheme();

  const toggleLanguage = () => {
    const currentLang = i18n.language;
    const currentIndex = supportedLanguages.indexOf(currentLang);
    const nextIndex = (currentIndex + 1) % supportedLanguages.length;
    i18n.changeLanguage(supportedLanguages[nextIndex]);
  };

  const toggleTheme = () => {
    setTheme(mode === "light" ? "dark" : "light");
  };

  return (
    <Stack spacing={1.5}>
      <Box
        sx={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
        }}
      >
        <Stack spacing={0.5} sx={{ flex: 1 }}>
          <Typography variant="h4" fontWeight={800}>
            {t("title")}
          </Typography>
          <Typography variant="body1" color="text.secondary">
            {t("subtitle")}
          </Typography>
        </Stack>

        <Stack direction="row" spacing={0.5}>
          <Tooltip title={tCommon("userMenu.changeLanguage")}>
            <IconButton onClick={toggleLanguage} size="medium">
              <TranslateIcon />
            </IconButton>
          </Tooltip>
          <Tooltip
            title={
              mode === "light"
                ? tCommon("userMenu.darkMode")
                : tCommon("userMenu.lightMode")
            }
          >
            <IconButton onClick={toggleTheme} size="medium">
              {mode === "light" ? <DarkIcon /> : <LightIcon />}
            </IconButton>
          </Tooltip>
        </Stack>
      </Box>
    </Stack>
  );
}
