import type { GcTimelineBucketKind } from "@shared/api/adminApi";

export type TimelinePoint = {
  bucketStartUtc: string;
  chunkCount: number;
  sizeBytes: number;
};

export const MIN_SLOT_COUNT_BY_BUCKET: Record<GcTimelineBucketKind, number> = {
  day: 7,
  hour: 24,
};

export const formatDateTime = (value: string): string => {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(parsed);
};

export const formatCount = (value: number): string =>
  new Intl.NumberFormat().format(value);

const parseDateToUtc = (value: string): Date => {
  const withZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`;
  const parsed = new Date(withZone);

  if (!Number.isNaN(parsed.getTime())) {
    return parsed;
  }

  return new Date(value);
};

const toCalendarDayIndex = (value: Date, timeZone: string): number | null => {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone,
    year: "numeric",
    month: "numeric",
    day: "numeric",
  }).formatToParts(value);
  const year = Number(parts.find((part) => part.type === "year")?.value);
  const month = Number(parts.find((part) => part.type === "month")?.value);
  const day = Number(parts.find((part) => part.type === "day")?.value);

  if (![year, month, day].every(Number.isInteger)) {
    return null;
  }

  return Math.floor(Date.UTC(year, month - 1, day) / bucketStepMs("day"));
};

const bucketStepMs = (bucket: GcTimelineBucketKind): number =>
  bucket === "day" ? 24 * 60 * 60 * 1000 : 60 * 60 * 1000;

export const toBucketIndex = (
  value: string,
  bucket: GcTimelineBucketKind,
  timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone,
): number | null => {
  const parsed = parseDateToUtc(value);
  if (Number.isNaN(parsed.getTime())) {
    return null;
  }

  if (bucket === "day") {
    return toCalendarDayIndex(parsed, timeZone);
  }

  return Math.floor(parsed.getTime() / bucketStepMs(bucket));
};

export const fromBucketIndexToIso = (
  index: number,
  bucket: GcTimelineBucketKind,
): string => new Date(index * bucketStepMs(bucket)).toISOString();

export const formatSlotLabel = (
  value: string,
  bucket: GcTimelineBucketKind,
): string => {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  if (bucket === "day") {
    return new Intl.DateTimeFormat(undefined, {
      month: "short",
      day: "2-digit",
      timeZone: "UTC",
    }).format(parsed);
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
  }).format(parsed);
};
