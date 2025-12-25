import type { Resource, ResourceLanguage } from "i18next";

type LocaleModule = { default: Record<string, unknown> };
const localeModules = import.meta.glob<LocaleModule>("./*.json", {
  eager: true,
});

const resources: Resource = {};
const namespaces = new Set<string>();

for (const [path, mod] of Object.entries(localeModules)) {
  const match = path.match(/\.\/(.+)\.json$/);
  if (!match) {
    continue;
  }

  const lng = match[1];
  const data = mod.default as ResourceLanguage;
  resources[lng] = data;
  Object.keys(data).forEach((ns) => namespaces.add(ns));
}

export const i18nResources = resources;
export const supportedLanguages = Object.keys(resources);
export const allNamespaces = Array.from(namespaces);
export const defaultNS = "common" as const;
export const fallbackLng = "en" as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];
export default i18nResources;
