import i18next from "i18next";
import en from "../../locales/en.json";
import { isRecord } from "../utils/typeGuards";

type LocaleNamespace = keyof typeof en;

const resolveEnglish = (namespace: LocaleNamespace, key: string): string => {
  let cursor: unknown = en[namespace];

  for (const segment of key.split(".")) {
    if (!isRecord(cursor)) {
      return key;
    }

    cursor = cursor[segment];
  }

  return typeof cursor === "string" ? cursor : key;
};

export const translateError = (
  namespace: LocaleNamespace,
  key: string,
): string => {
  const fallback = resolveEnglish(namespace, key);

  if (!i18next.isInitialized) {
    return fallback;
  }

  if (!i18next.exists(key, { ns: namespace })) {
    return fallback;
  }

  const value = i18next.t(key, { ns: namespace });
  return typeof value === "string" && value.length > 0 ? value : fallback;
};
