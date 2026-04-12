import {
  defaultNS,
  fallbackLng,
  i18nResources,
  allNamespaces,
  supportedLanguages,
} from "./locales";
import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { LANGUAGE_STORAGE_KEY } from "./shared/config/storageKeys";

const languageDetectorOptions = {
  order: ["querystring", "sessionStorage", "navigator", "htmlTag"],
  lookupSessionStorage: LANGUAGE_STORAGE_KEY,
  convertDetectedLanguage: (lng: string): string =>
    lng.toLowerCase().split("-")[0],
  caches: ["sessionStorage"],
};

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    detection: languageDetectorOptions,
    resources: i18nResources,
    fallbackLng,
    supportedLngs: supportedLanguages,
    nonExplicitSupportedLngs: true,
    load: "languageOnly",
    ns: allNamespaces,
    defaultNS,
    debug: false,
    interpolation: {
      escapeValue: false,
    },
  });

export default i18n;
