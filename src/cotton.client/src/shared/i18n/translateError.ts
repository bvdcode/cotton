import i18next from "i18next";
import en from "../../locales/en.json";

type LocaleNamespace = keyof typeof en;

const resolveEnglish = (namespace: LocaleNamespace, key: string): string => {
  let cursor: unknown = en[namespace];

  for (const segment of key.split(".")) {
    if (cursor === null || typeof cursor !== "object") {
      return key;
    }

    cursor = (cursor as Record<string, unknown>)[segment];
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
