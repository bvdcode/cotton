#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const localesDir = resolve(here, "../src/locales");

const PLURAL_RE = /_(zero|one|two|few|many|other)$/;
const stripPlural = (key) => key.replace(PLURAL_RE, "");

const flatten = (obj, prefix = "") => {
  const out = {};

  for (const key of Object.keys(obj ?? {})) {
    const value = obj[key];
    const fullKey = prefix ? `${prefix}.${key}` : key;

    if (value && typeof value === "object" && !Array.isArray(value)) {
      Object.assign(out, flatten(value, fullKey));
    } else {
      out[fullKey] = value;
    }
  }

  return out;
};

const loadLocale = (language) =>
  flatten(
    JSON.parse(readFileSync(resolve(localesDir, `${language}.json`), "utf8")),
  );

const en = loadLocale("en");
const enKeys = Object.keys(en);
const enKeySet = new Set(enKeys);
const enBaseSet = new Set(enKeys.map(stripPlural));

let exitCode = 0;

for (const language of ["ru", "es"]) {
  const flat = loadLocale(language);
  const keys = Object.keys(flat);
  const keySet = new Set(keys);
  const baseSet = new Set(keys.map(stripPlural));
  const missing = enKeys.filter(
    (key) => !keySet.has(key) && !baseSet.has(stripPlural(key)),
  );
  const orphan = keys.filter(
    (key) => !enKeySet.has(key) && !enBaseSet.has(stripPlural(key)),
  );

  console.log(`\n=== ${language.toUpperCase()} ===`);
  console.log(`  total keys: ${keys.length} (EN has ${enKeys.length})`);
  console.log(`  missing from ${language}: ${missing.length}`);
  console.log(`  orphans in ${language}: ${orphan.length}`);

  if (orphan.length > 0) {
    exitCode = 1;
    console.log("\n  orphan keys (no EN counterpart):");
    for (const key of orphan) {
      console.log(`   - ${key}`);
    }
  }

  if (missing.length > 0 && process.env.VERBOSE === "1") {
    console.log("\n  missing keys (verbose):");
    for (const key of missing) {
      console.log(`   - ${key}`);
    }
  }
}

if (exitCode === 0) {
  console.log("\nOK: no orphan keys; missing keys are tracked but expected.");
} else {
  console.log(
    "\nFAIL: orphan keys present. Either rename them in EN or remove them.",
  );
}

process.exit(exitCode);
