import { describe, expect, it } from "vitest";
import {
  getUsernameError,
  isValidUsername,
  normalizeUsername,
} from "./username";

describe("normalizeUsername", () => {
  it("trims whitespace and lowercases", () => {
    expect(normalizeUsername("  Alice  ")).toBe("alice");
    expect(normalizeUsername("BoB42")).toBe("bob42");
  });
});

describe("isValidUsername", () => {
  it("accepts valid usernames", () => {
    expect(isValidUsername("alice")).toBe(true);
    expect(isValidUsername("bob_42")).toBe(true);
    expect(isValidUsername("a-b.c")).toBe(true);
    expect(isValidUsername("a" + "1".repeat(31))).toBe(true);
  });

  it("rejects invalid length, prefix, separator, and suffix cases", () => {
    expect(isValidUsername("a")).toBe(false);
    expect(isValidUsername("a".repeat(33))).toBe(false);
    expect(isValidUsername("1alice")).toBe(false);
    expect(isValidUsername(".alice")).toBe(false);
    expect(isValidUsername("a__b")).toBe(false);
    expect(isValidUsername("a.-b")).toBe(false);
    expect(isValidUsername("alice.")).toBe(false);
    expect(isValidUsername("alice-")).toBe(false);
  });
});

describe("getUsernameError", () => {
  it("returns null for pristine and valid values", () => {
    expect(getUsernameError("")).toBeNull();
    expect(getUsernameError("   ")).toBeNull();
    expect(getUsernameError("alice")).toBeNull();
  });

  it("returns specific errors for invalid values", () => {
    expect(getUsernameError("a")).toMatch(/between 2 and 32/);
    expect(getUsernameError("1nvalid")).toMatch(/start with a letter/);
  });
});
