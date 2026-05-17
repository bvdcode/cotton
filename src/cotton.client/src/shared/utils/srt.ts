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

export const parseSrt = (content: string): LrcLine[] => {
  if (!content) {
    return [];
  }

  const stripped =
    content.charCodeAt(0) === 0xfeff ? content.slice(1) : content;
  const lines = stripped.split(/\r?\n/);
  const cues: Cue[] = [];

  let index = 0;
  while (index < lines.length) {
    while (index < lines.length && lines[index]?.trim() === "") {
      index += 1;
    }
    if (index >= lines.length) {
      break;
    }

    if (INDEX_ONLY_RE.test(lines[index] ?? "")) {
      index += 1;
    }
    if (index >= lines.length) {
      break;
    }

    const timeMatch = RANGE_RE.exec(lines[index] ?? "");
    if (!timeMatch) {
      while (index < lines.length && lines[index]?.trim() !== "") {
        index += 1;
      }
      continue;
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
    index += 1;

    const textLines: string[] = [];
    while (index < lines.length && lines[index]?.trim() !== "") {
      textLines.push(lines[index] ?? "");
      index += 1;
    }

    if (!Number.isFinite(start) || !Number.isFinite(end) || start < 0) {
      continue;
    }

    cues.push({
      start,
      end: Math.max(end, start),
      text: textLines.join("\n").trimEnd(),
    });
  }

  cues.sort((a, b) => a.start - b.start);

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
