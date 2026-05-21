import type { LrcLine } from "./lrc";

const TIME_RE = /(\d{1,2}):(\d{2}):(\d{2})[,.](\d{1,3})/;
const RANGE_RE = new RegExp(
  `^\\s*${TIME_RE.source}\\s*-->\\s*${TIME_RE.source}`,
);
const INDEX_ONLY_RE = /^\s*\d+\s*$/;
const CUE_GAP_EPSILON_SECONDS = 0.1;

interface Cue {
  start: number;
  end: number;
  text: string;
}

const parseTimeParts = (
  hours: string,
  minutes: string,
  seconds: string,
  fraction: string,
): number => {
  const h = Number.parseInt(hours, 10);
  const m = Number.parseInt(minutes, 10);
  const s = Number.parseInt(seconds, 10);
  const ms = Number.parseInt(fraction.padEnd(3, "0").slice(0, 3), 10);

  if (
    !Number.isFinite(h) ||
    !Number.isFinite(m) ||
    !Number.isFinite(s) ||
    !Number.isFinite(ms)
  ) {
    return Number.NaN;
  }

  return h * 3600 + m * 60 + s + ms / 1000;
};

const stripBom = (content: string): string =>
  content.charCodeAt(0) === 0xfeff ? content.slice(1) : content;

const skipBlankLines = (lines: string[], index: number): number => {
  let nextIndex = index;
  while (nextIndex < lines.length && lines[nextIndex]?.trim() === "") {
    nextIndex += 1;
  }
  return nextIndex;
};

const skipInvalidCue = (lines: string[], index: number): number => {
  let nextIndex = index;
  while (nextIndex < lines.length && lines[nextIndex]?.trim() !== "") {
    nextIndex += 1;
  }
  return nextIndex;
};

const skipCueIndex = (lines: string[], index: number): number =>
  INDEX_ONLY_RE.test(lines[index] ?? "") ? index + 1 : index;

const parseRange = (line: string): { start: number; end: number } | null => {
  const timeMatch = RANGE_RE.exec(line);
  if (!timeMatch) {
    return null;
  }

  const start = parseTimeParts(
    timeMatch[1] ?? "",
    timeMatch[2] ?? "",
    timeMatch[3] ?? "",
    timeMatch[4] ?? "",
  );
  const end = parseTimeParts(
    timeMatch[5] ?? "",
    timeMatch[6] ?? "",
    timeMatch[7] ?? "",
    timeMatch[8] ?? "",
  );
  return { start, end };
};

const readCueText = (
  lines: string[],
  index: number,
): { text: string; nextIndex: number } => {
  const textLines: string[] = [];
  let nextIndex = index;
  while (nextIndex < lines.length && lines[nextIndex]?.trim() !== "") {
    textLines.push(lines[nextIndex] ?? "");
    nextIndex += 1;
  }

  return { text: textLines.join("\n").trimEnd(), nextIndex };
};

const isValidCueRange = (start: number, end: number): boolean =>
  Number.isFinite(start) && Number.isFinite(end) && start >= 0;

const readNextCue = (
  lines: string[],
  index: number,
): { cue: Cue | null; nextIndex: number } => {
  let nextIndex = skipBlankLines(lines, index);
  if (nextIndex >= lines.length) {
    return { cue: null, nextIndex };
  }

  nextIndex = skipCueIndex(lines, nextIndex);
  if (nextIndex >= lines.length) {
    return { cue: null, nextIndex };
  }

  const range = parseRange(lines[nextIndex] ?? "");
  if (!range) {
    return { cue: null, nextIndex: skipInvalidCue(lines, nextIndex) };
  }

  const text = readCueText(lines, nextIndex + 1);
  if (!isValidCueRange(range.start, range.end)) {
    return { cue: null, nextIndex: text.nextIndex };
  }

  return {
    cue: {
      start: range.start,
      end: Math.max(range.end, range.start),
      text: text.text,
    },
    nextIndex: text.nextIndex,
  };
};

const toLrcLines = (cues: Cue[]): LrcLine[] => {
  const result: LrcLine[] = [];
  for (let index = 0; index < cues.length; index += 1) {
    const cue = cues[index];
    if (!cue) {
      continue;
    }

    result.push({ timeSeconds: cue.start, text: cue.text });

    const next = cues[index + 1];
    if (!next || next.start - cue.end > CUE_GAP_EPSILON_SECONDS) {
      result.push({ timeSeconds: cue.end, text: "" });
    }
  }

  return result;
};

export const parseSrt = (content: string): LrcLine[] => {
  if (!content) {
    return [];
  }

  const lines = stripBom(content).split(/\r?\n/);
  const cues: Cue[] = [];

  let index = 0;
  while (index < lines.length) {
    const parsed = readNextCue(lines, index);
    if (parsed.cue) {
      cues.push(parsed.cue);
    }

    index = Math.max(parsed.nextIndex, index + 1);
  }

  cues.sort((a, b) => a.start - b.start);
  return toLrcLines(cues);
};
