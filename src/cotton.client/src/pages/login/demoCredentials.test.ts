import { describe, expect, it } from "vitest";
import {
  DEMO_CREDENTIALS_STORAGE_KEY,
  generateDemoCredentials,
  getOrCreateDemoCredentials,
  isDemoCredentials,
} from "./demoCredentials";

class MemoryStorage implements Pick<Storage, "getItem" | "setItem"> {
  private readonly items = new Map<string, string>();

  getItem(key: string): string | null {
    return this.items.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.items.set(key, value);
  }
}

describe("demoCredentials", () => {
  it("generates valid public-instance login credentials", () => {
    const credentials = generateDemoCredentials();

    expect(credentials.username).toMatch(/^demo[a-z0-9]{16}$/);
    expect(credentials.password).toHaveLength(32);
    expect(isDemoCredentials(credentials)).toBe(true);
  });

  it("reuses stored credentials for the same browser", () => {
    const storage = new MemoryStorage();

    const first = getOrCreateDemoCredentials(storage);
    const second = getOrCreateDemoCredentials(storage);

    expect(second).toEqual(first);
  });

  it("replaces malformed stored credentials", () => {
    const storage = new MemoryStorage();
    storage.setItem(DEMO_CREDENTIALS_STORAGE_KEY, JSON.stringify({
      username: "demo",
      password: "demo",
    }));

    const credentials = getOrCreateDemoCredentials(storage);

    expect(credentials.username).toMatch(/^demo[a-z0-9]{16}$/);
    expect(credentials.password).toHaveLength(32);
  });
});
