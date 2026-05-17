import { describe, expect, it } from "vitest";
import {
  formatDateOnly,
  getAgeYears,
  toDateInputValue,
  tryParseDateOnly,
} from "./dateOnly";

describe("toDateInputValue", () => {
  it("returns empty string for empty values", () => {
    expect(toDateInputValue(null)).toBe("");
    expect(toDateInputValue(undefined)).toBe("");
    expect(toDateInputValue("")).toBe("");
    expect(toDateInputValue("   ")).toBe("");
  });

  it("extracts a YYYY-MM-DD prefix", () => {
    expect(toDateInputValue("2026-05-13")).toBe("2026-05-13");
    expect(toDateInputValue("2026-05-13T12:34:56Z")).toBe("2026-05-13");
  });

  it("returns empty string when no date prefix is present", () => {
    expect(toDateInputValue("not a date")).toBe("");
  });
});

describe("tryParseDateOnly", () => {
  it("parses a valid date", () => {
    const parsed = tryParseDateOnly("2026-05-13");

    expect(parsed).not.toBeNull();
    expect(parsed?.getFullYear()).toBe(2026);
    expect(parsed?.getMonth()).toBe(4);
    expect(parsed?.getDate()).toBe(13);
  });

  it("rejects invalid dates", () => {
    expect(tryParseDateOnly("2026-13-01")).toBeNull();
    expect(tryParseDateOnly("2026-02-30")).toBeNull();
    expect(tryParseDateOnly("not a date")).toBeNull();
  });
});

describe("formatDateOnly", () => {
  it("returns the original value for unparseable input", () => {
    expect(formatDateOnly("not a date")).toBe("not a date");
  });

  it("formats a date-only value", () => {
    const formatted = formatDateOnly("2026-05-13");

    expect(formatted).not.toBe("2026-05-13");
    expect(formatted.length).toBeGreaterThan(0);
  });
});

describe("getAgeYears", () => {
  it("counts full years between two dates", () => {
    expect(getAgeYears(new Date(2000, 5, 1), new Date(2026, 5, 1))).toBe(26);
  });

  it("subtracts a year when birthday has not happened yet", () => {
    expect(getAgeYears(new Date(2000, 11, 31), new Date(2026, 5, 1))).toBe(
      25,
    );
    expect(getAgeYears(new Date(2000, 5, 15), new Date(2026, 5, 14))).toBe(
      25,
    );
  });
});
