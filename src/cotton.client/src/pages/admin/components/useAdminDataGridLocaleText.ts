import {
  csCZ,
  deDE,
  enUS,
  esES,
  frFR,
  itIT,
  nlNL,
  plPL,
  ptPT,
  ruRU,
  ukUA,
  zhCN,
} from "@mui/x-data-grid/locales";
import type { GridLocaleText } from "@mui/x-data-grid";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

const gridLocales = {
  cs: csCZ,
  de: deDE,
  en: enUS,
  es: esES,
  fr: frFR,
  it: itIT,
  nl: nlNL,
  pl: plPL,
  pt: ptPT,
  ru: ruRU,
  uk: ukUA,
  zh: zhCN,
};

export const getAdminDataGridLocaleText = (
  language: string,
  noRowsLabel: string,
): Partial<GridLocaleText> => {
  const languageCode = language.toLowerCase().split("-")[0];
  const localization =
    gridLocales[languageCode as keyof typeof gridLocales] ?? enUS;

  return {
    ...localization.components.MuiDataGrid.defaultProps.localeText,
    noRowsLabel,
  };
};

export const useAdminDataGridLocaleText = (
  noRowsLabel: string,
): Partial<GridLocaleText> => {
  const { i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;

  return useMemo(
    () => getAdminDataGridLocaleText(language, noRowsLabel),
    [language, noRowsLabel],
  );
};
