import { describe, expect, it } from "vitest";
import {
  isEmail,
  normalizeTwoFactorCode,
  tryGetTwoFactorHint,
} from "./loginUtils";

describe("loginUtils", () => {
  it("normalizes two-factor codes to six digits", () => {
    expect(normalizeTwoFactorCode("12 3a45-678")).toBe("123456");
  });

  it("validates email-shaped usernames", () => {
    expect(isEmail(" user@example.com ")).toBe(true);
    expect(isEmail("user")).toBe(false);
    expect(isEmail("user@example")).toBe(false);
  });

  it("detects two-factor server hints from 403 messages", () => {
    expect(
      tryGetTwoFactorHint({
        status: 403,
        serverMessage: "Two-factor code required",
      }),
    ).toBe("required");
    expect(
      tryGetTwoFactorHint({
        status: 403,
        serverMessage: "Invalid two-factor code",
      }),
    ).toBe("invalid");
    expect(
      tryGetTwoFactorHint({
        status: 403,
        serverMessage: "Maximum attempts reached",
      }),
    ).toBe("locked");
  });

  it("ignores non-403 messages", () => {
    expect(
      tryGetTwoFactorHint({
        status: 401,
        serverMessage: "Two-factor code required",
      }),
    ).toBeNull();
  });
});
