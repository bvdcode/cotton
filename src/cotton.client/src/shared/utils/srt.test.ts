import { describe, expect, it } from "vitest";
import { findActiveLrcLineIndex } from "./lrc";
import { parseSrt } from "./srt";

describe("parseSrt", () => {
  it("returns an empty list for empty input", () => {
    expect(parseSrt("")).toEqual([]);
  });

  it("parses a basic two-cue file", () => {
    const result = parseSrt(`1
00:00:00,000 --> 00:00:02,000
Hello

2
00:00:02,000 --> 00:00:04,500
Another line
`);

    expect(result).toEqual([
      { timeSeconds: 0, text: "Hello" },
      { timeSeconds: 2, text: "Another line" },
      { timeSeconds: 4.5, text: "" },
    ]);
  });

  it("clears the line between separated cues", () => {
    const result = parseSrt(`1
00:00:00,000 --> 00:00:01,000
First

2
00:00:05,000 --> 00:00:06,000
Second
`);

    expect(result).toEqual([
      { timeSeconds: 0, text: "First" },
      { timeSeconds: 1, text: "" },
      { timeSeconds: 5, text: "Second" },
      { timeSeconds: 6, text: "" },
    ]);
  });

  it("joins multi-line cue text with newlines", () => {
    const [first] = parseSrt(`1
00:00:00,000 --> 00:00:02,000
Line A
Line B
`);

    expect(first?.text).toBe("Line A\nLine B");
  });

  it("accepts dot decimal timestamps", () => {
    const [first] = parseSrt(`1
00:00:00.500 --> 00:00:02.250
Dot decimal
`);

    expect(first?.timeSeconds).toBeCloseTo(0.5, 5);
    expect(first?.text).toBe("Dot decimal");
  });

  it("accepts a missing index line", () => {
    const [first] = parseSrt(`00:00:01,000 --> 00:00:02,000
No index
`);

    expect(first?.timeSeconds).toBe(1);
    expect(first?.text).toBe("No index");
  });

  it("strips a leading byte-order mark", () => {
    const [first] = parseSrt(
      "\uFEFF1\n00:00:00,000 --> 00:00:01,000\nWith BOM\n",
    );

    expect(first?.text).toBe("With BOM");
  });

  it("handles CRLF line endings", () => {
    const [first] = parseSrt(
      "1\r\n00:00:00,000 --> 00:00:01,000\r\nCRLF cue\r\n",
    );

    expect(first?.text).toBe("CRLF cue");
  });

  it("skips malformed cues without dropping later cues", () => {
    const result = parseSrt(`1
not a time line
broken

2
00:00:10,000 --> 00:00:11,000
Still here
`);

    expect(result.find((entry) => entry.text === "Still here")).toEqual({
      timeSeconds: 10,
      text: "Still here",
    });
  });

  it("sorts cues by start time", () => {
    const result = parseSrt(`2
00:00:10,000 --> 00:00:11,000
Later

1
00:00:01,000 --> 00:00:02,000
Earlier
`);

    expect(result.map((entry) => entry.text)).toEqual([
      "Earlier",
      "",
      "Later",
      "",
    ]);
  });

  it("works with the active line lookup used by the player", () => {
    const lines = parseSrt(`1
00:00:00,000 --> 00:00:02,000
Greeting

2
00:00:05,000 --> 00:00:06,000
Farewell
`);

    expect(lines[findActiveLrcLineIndex(lines, 1)]?.text).toBe("Greeting");
    expect(lines[findActiveLrcLineIndex(lines, 3)]?.text).toBe("");
    expect(lines[findActiveLrcLineIndex(lines, 5.5)]?.text).toBe("Farewell");
  });
});
