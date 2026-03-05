export type LrcLine = {
  timeSeconds: number;
  text: string;
};

const TIMESTAMP_RE = /\[(\d+):(\d+)(?:\.(\d+))?\]/g;

const parseFraction = (raw: string | undefined): number => {
  if (!raw) return 0;

  const digits = raw.trim();
  if (!digits) return 0;

  const value = Number.parseInt(digits, 10);
  if (!Number.isFinite(value) || value < 0) return 0;

  // Common LRC: .xx is centiseconds, .xxx is milliseconds.
  if (digits.length === 2) return value / 100;
  if (digits.length === 3) return value / 1000;

  // Fallback: treat as milliseconds-like.
  return value / Math.pow(10, digits.length);
};

/**
 * Parses .lrc lyrics into timestamped lines.
 *
 * Supports multiple timestamps per line.
 * Ignores metadata tags like [ar:...].
 */
export const parseLrc = (content: string): LrcLine[] => {
  const lines = content.split(/\r?\n/);
  const result: LrcLine[] = [];

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();
    if (!line) continue;

    const timestamps: number[] = [];
    TIMESTAMP_RE.lastIndex = 0;
    let match: RegExpExecArray | null;

    while ((match = TIMESTAMP_RE.exec(line))) {
      const minutes = Number.parseInt(match[1] ?? "", 10);
      const seconds = Number.parseInt(match[2] ?? "", 10);
      const fraction = parseFraction(match[3]);

      if (!Number.isFinite(minutes) || !Number.isFinite(seconds)) {
        continue;
      }

      const timeSeconds = minutes * 60 + seconds + fraction;
      if (!Number.isFinite(timeSeconds) || timeSeconds < 0) {
        continue;
      }

      timestamps.push(timeSeconds);
    }

    if (timestamps.length === 0) {
      continue;
    }

    const text = line.replace(TIMESTAMP_RE, "").trim();
    // Allow empty text (instrumental / breaks), but keep as a blank line.

    for (const timeSeconds of timestamps) {
      result.push({ timeSeconds, text });
    }
  }

  result.sort((a, b) => a.timeSeconds - b.timeSeconds);
  return result;
};

export const findActiveLrcLineIndex = (
  lines: ReadonlyArray<LrcLine>,
  timeSeconds: number,
): number => {
  if (lines.length === 0) return 0;

  let lo = 0;
  let hi = lines.length;

  while (lo < hi) {
    const mid = Math.floor((lo + hi) / 2);
    const t = lines[mid]?.timeSeconds ?? 0;
    if (t <= timeSeconds) {
      lo = mid + 1;
    } else {
      hi = mid;
    }
  }

  const idx = lo - 1;
  if (idx < 0) return 0;
  if (idx >= lines.length) return lines.length - 1;
  return idx;
};
