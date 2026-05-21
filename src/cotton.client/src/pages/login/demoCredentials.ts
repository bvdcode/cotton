import { STORAGE_KEY_PREFIX } from "../../shared/config/storageKeys";

export interface DemoCredentials {
  username: string;
  password: string;
  firstName: string;
  lastName: string;
}

const passwordLength = 32;
const alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
const usernameEntropyLength = 6;

const demoFirstNames = [
  "Amber",
  "Brave",
  "Bright",
  "Calm",
  "Cosmic",
  "Frosty",
  "Gentle",
  "Golden",
  "Lucky",
  "Mint",
  "Orange",
  "Quiet",
  "Rapid",
  "Silver",
  "Sunny",
  "Velvet",
] as const;

const demoLastNames = [
  "Badger",
  "Capybara",
  "Falcon",
  "Fox",
  "Koala",
  "Lemur",
  "Lynx",
  "Marten",
  "Otter",
  "Panda",
  "Quokka",
  "Raven",
  "Seal",
  "Sparrow",
  "Tiger",
  "Wombat",
] as const;

export const DEMO_CREDENTIALS_STORAGE_KEY = STORAGE_KEY_PREFIX + "demo-credentials";

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const demoUsernameRegex = /^u_[a-z0-9]{6}$/;
const demoNameRegex = /^[A-Z][a-z]{1,31}$/;

export const isDemoCredentials = (value: unknown): value is DemoCredentials => {
  if (!isRecord(value)) {
    return false;
  }

  return (
    typeof value.username === "string" &&
    typeof value.password === "string" &&
    typeof value.firstName === "string" &&
    typeof value.lastName === "string" &&
    demoUsernameRegex.test(value.username) &&
    value.password.length === passwordLength &&
    demoNameRegex.test(value.firstName) &&
    demoNameRegex.test(value.lastName)
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

export const generateDemoCredentials = (): DemoCredentials => ({
  username: "u_" + randomString(usernameEntropyLength),
  password: randomString(passwordLength),
  firstName: randomItem(demoFirstNames),
  lastName: randomItem(demoLastNames),
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
