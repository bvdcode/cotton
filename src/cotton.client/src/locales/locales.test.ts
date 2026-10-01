import { describe, expect, it } from "vitest";
import cs from "./cs.json";
import de from "./de.json";
import en from "./en.json";
import es from "./es.json";
import fr from "./fr.json";
import itLocale from "./it.json";
import { supportedLanguages } from ".";
import { nativeLanguageNames } from "./languageDisplayNames";
import nl from "./nl.json";
import pl from "./pl.json";
import pt from "./pt.json";
import ru from "./ru.json";
import uk from "./uk.json";
import zh from "./zh.json";

type LocaleObject = object;

const PLURAL_SUFFIX_RE = /_(zero|one|two|few|many|other)$/;

const baseKey = (key: string): string => key.replace(PLURAL_SUFFIX_RE, "");

const flatten = (obj: LocaleObject, prefix = ""): Record<string, string> => {
  const out: Record<string, string> = {};

  for (const [key, value] of Object.entries(obj)) {
    const fullKey = prefix ? `${prefix}.${key}` : key;

    if (value !== null && typeof value === "object" && !Array.isArray(value)) {
      Object.assign(out, flatten(value, fullKey));
    } else if (typeof value === "string") {
      out[fullKey] = value;
    }
  }

  return out;
};

const flatEn = flatten(en);
const enKeys = new Set(Object.keys(flatEn));
const enBaseKeys = new Set([...enKeys].map(baseKey));

const findOrphans = (locale: Record<string, string>): string[] =>
  Object.keys(locale).filter(
    (key) => !enKeys.has(key) && !enBaseKeys.has(baseKey(key)),
  );

const findMissing = (locale: Record<string, string>): string[] =>
  [...enKeys].filter((key) => !(key in locale));

const requiredGalleryUndoKeys = [
  "common.actions.undo",
  "files.preview.deleteToast",
  "files.preview.deleteUndoFailed",
] as const;

const getLocaleValue = (locale: LocaleObject, path: string): string | null => {
  let current: object | string | null = locale;
  for (const segment of path.split(".")) {
    if (current === null || typeof current !== "object") {
      return null;
    }
    const entry: [string, string | object | null] | undefined =
      Object.entries(current).find(([key]) => key === segment);
    if (!entry) {
      return null;
    }
    current = entry[1];
  }
  return typeof current === "string" ? current : null;
};

const nonEnLocales: ReadonlyArray<readonly [string, LocaleObject]> = [
  ["Czech", cs],
  ["German", de],
  ["Spanish", es],
  ["French", fr],
  ["Italian", itLocale],
  ["Dutch", nl],
  ["Polish", pl],
  ["Portuguese", pt],
  ["Russian", ru],
  ["Ukrainian", uk],
  ["Chinese", zh],
] as const;

describe("locale parity", () => {
  it("keeps native display names aligned with supported languages", () => {
    expect(Object.keys(nativeLanguageNames).sort()).toEqual(
      [...supportedLanguages].sort(),
    );
  });

  it("keeps EN string leaves non-empty", () => {
    for (const [key, value] of Object.entries(flatEn)) {
      if (typeof value !== "string") continue;

      expect(
        value.length > 0,
        `en.json[${key}] must not be an empty string`,
      ).toBe(true);
    }
  });

  for (const [language, locale] of nonEnLocales) {
    it(`keeps ${language} keys inside the EN namespace, modulo plural suffixes`, () => {
      const orphan = findOrphans(flatten(locale));

      expect(
        orphan,
        `${language} locale contains keys without an EN counterpart: ${orphan.join(", ")}`,
      ).toEqual([]);
    });

    it(`keeps ${language} complete with the EN namespace`, () => {
      const missing = findMissing(flatten(locale));

      expect(
        missing,
        `${language} locale is missing EN keys: ${missing.join(", ")}`,
      ).toEqual([]);
    });

    it(`keeps ${language} gallery undo strings translated`, () => {
      for (const key of requiredGalleryUndoKeys) {
        const value = getLocaleValue(locale, key);

        expect(
          typeof value === "string" && value.length > 0,
          `${language} locale is missing ${key}`,
        ).toBe(true);
      }
    });
  }
});
