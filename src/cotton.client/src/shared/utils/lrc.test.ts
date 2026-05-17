import { describe, expect, it } from "vitest";
import { findActiveLrcLineIndex, parseLrc } from "./lrc";

describe("parseLrc", () => {
  it("returns an empty list for empty input", () => {
    expect(parseLrc("")).toEqual([]);
  });

  it("parses a basic line with an mm:ss timestamp", () => {
    expect(parseLrc("[00:12]Hello world")).toEqual([
      { timeSeconds: 12, text: "Hello world" },
    ]);
  });

  it("handles centisecond and millisecond fractions", () => {
    const [centisecond] = parseLrc("[01:23.45]Lyric");
    const [millisecond] = parseLrc("[00:01.250]Tick");

    expect(centisecond?.timeSeconds).toBeCloseTo(83.45, 5);
    expect(centisecond?.text).toBe("Lyric");
    expect(millisecond?.timeSeconds).toBeCloseTo(1.25, 5);
  });

  it("expands multiple timestamps on the same line", () => {
    expect(parseLrc("[00:05][00:10]Chorus")).toEqual([
      { timeSeconds: 5, text: "Chorus" },
      { timeSeconds: 10, text: "Chorus" },
    ]);
  });

  it("skips metadata tags and sorts timestamped lines", () => {
    const lines = parseLrc(
      "[ar: Artist]\n[00:30]Three\n[00:10]One\n[00:20]Two",
    );

    expect(lines.map((line) => line.text)).toEqual(["One", "Two", "Three"]);
  });

  it("keeps timestamped instrumental breaks", () => {
    expect(parseLrc("[00:42]")).toEqual([{ timeSeconds: 42, text: "" }]);
  });
});

describe("findActiveLrcLineIndex", () => {
  const lines = [
    { timeSeconds: 0, text: "a" },
    { timeSeconds: 5, text: "b" },
    { timeSeconds: 10, text: "c" },
    { timeSeconds: 15, text: "d" },
  ];

  it("returns 0 for an empty list", () => {
    expect(findActiveLrcLineIndex([], 12.5)).toBe(0);
  });

  it("returns the latest line whose timestamp is not after the current time", () => {
    expect(findActiveLrcLineIndex(lines, -1)).toBe(0);
    expect(findActiveLrcLineIndex(lines, 0)).toBe(0);
    expect(findActiveLrcLineIndex(lines, 4.999)).toBe(0);
    expect(findActiveLrcLineIndex(lines, 5)).toBe(1);
    expect(findActiveLrcLineIndex(lines, 12)).toBe(2);
    expect(findActiveLrcLineIndex(lines, 100)).toBe(3);
  });
});
