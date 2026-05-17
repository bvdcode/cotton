import { describe, expect, it } from "vitest";
import {
  fromBucketIndexToIso,
  toBucketIndex,
} from "./timelineUtils";

describe("storage statistics timeline utils", () => {
  it("normalizes zoneless timestamps as UTC", () => {
    expect(toBucketIndex("2026-05-16T10:45:00", "hour")).toBe(
      toBucketIndex("2026-05-16T10:45:00Z", "hour"),
    );
  });

  it("rounds timestamps down to the selected bucket", () => {
    const hourIndex = toBucketIndex("2026-05-16T10:45:00Z", "hour");
    const dayIndex = toBucketIndex("2026-05-16T10:45:00Z", "day");

    expect(hourIndex).not.toBeNull();
    expect(dayIndex).not.toBeNull();
    expect(fromBucketIndexToIso(hourIndex ?? 0, "hour")).toBe(
      "2026-05-16T10:00:00.000Z",
    );
    expect(fromBucketIndexToIso(dayIndex ?? 0, "day")).toBe(
      "2026-05-16T00:00:00.000Z",
    );
  });

  it("returns null for invalid timestamps", () => {
    expect(toBucketIndex("not-a-date", "hour")).toBeNull();
  });
});
