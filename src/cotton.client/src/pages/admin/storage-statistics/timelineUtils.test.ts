import { describe, expect, it } from "vitest";
import { fromBucketIndexToIso, toBucketIndex } from "./timelineUtils";

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

  it("uses the local calendar date for daily buckets east of UTC", () => {
    const index = toBucketIndex(
      "2026-05-04T19:00:00.000Z",
      "day",
      "Asia/Tashkent",
    );

    expect(index).not.toBeNull();
    expect(fromBucketIndexToIso(index ?? 0, "day")).toBe(
      "2026-05-05T00:00:00.000Z",
    );
  });

  it("uses the local calendar date for daily buckets west of UTC", () => {
    const index = toBucketIndex(
      "2026-05-05T07:00:00.000Z",
      "day",
      "America/Los_Angeles",
    );

    expect(index).not.toBeNull();
    expect(fromBucketIndexToIso(index ?? 0, "day")).toBe(
      "2026-05-05T00:00:00.000Z",
    );
  });

  it("keeps consecutive local days adjacent across daylight saving time", () => {
    const beforeTransition = toBucketIndex(
      "2026-03-08T08:00:00.000Z",
      "day",
      "America/Los_Angeles",
    );
    const afterTransition = toBucketIndex(
      "2026-03-09T07:00:00.000Z",
      "day",
      "America/Los_Angeles",
    );

    expect(beforeTransition).not.toBeNull();
    expect(afterTransition).toBe((beforeTransition ?? 0) + 1);
  });
});
