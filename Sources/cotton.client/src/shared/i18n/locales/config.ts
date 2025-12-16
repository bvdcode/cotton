import i18n from "i18next";
import { resources } from ".";
import { initReactI18next } from "react-i18next";
import { STORAGE_KEY_PREFIX } from "../../config/storageKeys";
import LanguageDetector from "i18next-browser-languagedetector";

const supportedLangs = Object.keys(resources);

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: "en",
    resources: resources,
    supportedLngs: supportedLangs,
    nonExplicitSupportedLngs: true,
    defaultNS: "common",
    detection: {
      order: ["localStorage", "navigator", "htmlTag"],
      caches: ["localStorage"],
      lookupLocalStorage: STORAGE_KEY_PREFIX + "i18next-lang",
    },
    interpolation: {
      escapeValue: false,
    },
  });

export default i18n;
