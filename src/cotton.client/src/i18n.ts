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

const languageDetectorOptions = {
  order: ["localStorage", "navigator", "querystring", "cookie", "htmlTag"],
  lookupLocalStorage: "ctn-i18nextLng",
  lookupFromNavigatorLanguage: true,
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
