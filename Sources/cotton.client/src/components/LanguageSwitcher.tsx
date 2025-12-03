import { Language } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { ListItemButton, ListItemIcon, ListItemText } from "@mui/material";

export default function LanguageSwitcher() {
  const { t, i18n } = useTranslation();

  const getSupportedLangs = (): string[] => {
    const raw = (i18n.options?.supportedLngs as unknown as string[]) || [];
    const filtered = raw.filter((l) => typeof l === "string" && l !== "cimode");
    return filtered.length
      ? filtered
      : [i18n.resolvedLanguage || i18n.language];
  };

  const handleClick = () => {
    const langs = getSupportedLangs();
    const current = i18n.resolvedLanguage || i18n.language;
    const idx = Math.max(0, langs.indexOf(current));
    const next = langs[(idx + 1) % langs.length];
    if (next && next !== current) {
      i18n.changeLanguage(next);
    }
  };

  return (
    <ListItemButton onClick={handleClick}>
      <ListItemIcon>
        <Language />
      </ListItemIcon>
      <ListItemText primary={t("navigation.language")} />
    </ListItemButton>
  );
}
