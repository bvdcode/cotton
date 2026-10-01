import i18next from "i18next";
import en from "../../locales/en.json";

type LocaleNamespace = keyof typeof en;

const resolveEnglish = (namespace: LocaleNamespace, key: string): string => {
  let cursor: object | string = en[namespace];

  for (const segment of key.split(".")) {
    if (typeof cursor !== "object" || cursor === null) {
      return key;
    }
    const next: object | string | null | undefined = Object.entries(cursor)
      .find(([entryKey]) => entryKey === segment)?.[1];
    if ((typeof next !== "object" && typeof next !== "string") || next === null) {
      return key;
    }
    cursor = next;
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
