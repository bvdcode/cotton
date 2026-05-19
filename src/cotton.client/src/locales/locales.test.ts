import { describe, expect, it } from "vitest";
import de from "./de.json";
import en from "./en.json";
import es from "./es.json";
import ru from "./ru.json";

type LocaleObject = Record<string, unknown>;

const PLURAL_SUFFIX_RE = /_(zero|one|two|few|many|other)$/;

const baseKey = (key: string): string => key.replace(PLURAL_SUFFIX_RE, "");

const flatten = (
  obj: LocaleObject,
  prefix = "",
): Record<string, unknown> => {
  const out: Record<string, unknown> = {};

  for (const key of Object.keys(obj)) {
    const value = obj[key];
    const fullKey = prefix ? `${prefix}.${key}` : key;

    if (
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value)
    ) {
      Object.assign(out, flatten(value as LocaleObject, fullKey));
    } else {
      out[fullKey] = value;
    }
  }

  return out;
};

const flatEn = flatten(en as LocaleObject);
const flatRu = flatten(ru as LocaleObject);
const flatEs = flatten(es as LocaleObject);
const flatDe = flatten(de as LocaleObject);
const enKeys = new Set(Object.keys(flatEn));
const enBaseKeys = new Set([...enKeys].map(baseKey));

const findOrphans = (locale: Record<string, unknown>): string[] =>
  Object.keys(locale).filter(
    (key) => !enKeys.has(key) && !enBaseKeys.has(baseKey(key)),
  );

describe("locale parity", () => {
  it("keeps EN string leaves non-empty", () => {
    for (const [key, value] of Object.entries(flatEn)) {
      if (typeof value !== "string") continue;

      expect(
        value.length > 0,
        `en.json[${key}] must not be an empty string`,
      ).toBe(true);
    }
  });

  it("keeps RU keys inside the EN namespace, modulo plural suffixes", () => {
    const orphan = findOrphans(flatRu);

    expect(
      orphan,
      `Russian locale contains keys without an EN counterpart: ${orphan.join(", ")}`,
    ).toEqual([]);
  });

  it("keeps ES keys inside the EN namespace, modulo plural suffixes", () => {
    const orphan = findOrphans(flatEs);

    expect(
      orphan,
      `Spanish locale contains keys without an EN counterpart: ${orphan.join(", ")}`,
    ).toEqual([]);
  });

  it("keeps DE keys inside the EN namespace, modulo plural suffixes", () => {
    const orphan = findOrphans(flatDe);

    expect(
      orphan,
      `German locale contains keys without an EN counterpart: ${orphan.join(", ")}`,
    ).toEqual([]);
  });
});
