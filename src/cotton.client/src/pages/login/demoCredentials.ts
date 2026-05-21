import { STORAGE_KEY_PREFIX } from "../../shared/config/storageKeys";

export interface DemoCredentials {
  username: string;
  password: string;
}

const passwordLength = 32;
const alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
const usernameNumberRange = 1000;
const usernameNumberLength = 3;

const demoAdjectives = [
  "amber",
  "brave",
  "bright",
  "calm",
  "cosmic",
  "frosty",
  "gentle",
  "golden",
  "lucky",
  "mint",
  "orange",
  "quiet",
  "rapid",
  "silver",
  "sunny",
  "velvet",
] as const;

const demoAnimals = [
  "badger",
  "capybara",
  "falcon",
  "fox",
  "koala",
  "lemur",
  "lynx",
  "marten",
  "otter",
  "panda",
  "quokka",
  "raven",
  "seal",
  "sparrow",
  "tiger",
  "wombat",
] as const;

export const DEMO_CREDENTIALS_STORAGE_KEY = STORAGE_KEY_PREFIX + "demo-credentials";

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const demoUsernameRegex = /^[a-z]+_[a-z]+_[0-9]{3}$/;

export const isDemoCredentials = (value: unknown): value is DemoCredentials => {
  if (!isRecord(value)) {
    return false;
  }

  return (
    typeof value.username === "string" &&
    typeof value.password === "string" &&
    demoUsernameRegex.test(value.username) &&
    value.password.length === passwordLength
  );
};

const getRandomByte = (): number => {
  const crypto = globalThis.crypto;
  if (crypto?.getRandomValues) {
    return crypto.getRandomValues(new Uint8Array(1))[0];
  }

  return Math.floor(Math.random() * 256);
};

const randomString = (length: number): string => {
  let value = "";
  for (let index = 0; index < length; index += 1) {
    value += alphabet[getRandomByte() % alphabet.length];
  }

  return value;
};

const randomItem = <T>(items: readonly T[]): T =>
  items[getRandomByte() % items.length];

const randomInt = (maxExclusive: number): number =>
  ((getRandomByte() << 8) | getRandomByte()) % maxExclusive;

const randomUsernameNumber = (): string =>
  String(randomInt(usernameNumberRange)).padStart(usernameNumberLength, "0");

export const generateDemoCredentials = (): DemoCredentials => ({
  username: [
    randomItem(demoAdjectives),
    randomItem(demoAnimals),
    randomUsernameNumber(),
  ].join("_"),
  password: randomString(passwordLength),
});

export const readStoredDemoCredentials = (
  storage: Pick<Storage, "getItem">,
): DemoCredentials | null => {
  try {
    const raw = storage.getItem(DEMO_CREDENTIALS_STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const parsed: unknown = JSON.parse(raw);
    return isDemoCredentials(parsed) ? parsed : null;
  } catch {
    return null;
  }
};

export const getOrCreateDemoCredentials = (
  storage: Pick<Storage, "getItem" | "setItem">,
): DemoCredentials => {
  const stored = readStoredDemoCredentials(storage);
  if (stored) {
    return stored;
  }

  const created = generateDemoCredentials();
  try {
    storage.setItem(DEMO_CREDENTIALS_STORAGE_KEY, JSON.stringify(created));
  } catch {
    // Demo login should still work in private contexts where localStorage writes fail.
  }

  return created;
};
