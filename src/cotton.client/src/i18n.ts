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
import { STORAGE_KEY_PREFIX } from "./shared/config/storageKeys";

const languageDetectorOptions = {
  order: ["querystring", "localStorage", "navigator", "htmlTag"],
  lookupLocalStorage: STORAGE_KEY_PREFIX + "language",
  caches: ["localStorage"],
};

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    detection: languageDetectorOptions,
    resources: i18nResources,
    fallbackLng,
    supportedLngs: supportedLanguages,
    ns: allNamespaces,
    defaultNS,
    debug: false,
    interpolation: {
      escapeValue: false,
    },
  });

export default i18n;
