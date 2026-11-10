import { shareLinkExpirationPresets } from "./shareLinkExpiration";

export interface ShareLinkTarget {
  kind: "file" | "folder";
  id: string;
  name: string;
}

export const shareLinkDurationUnits = [
  { key: "years", minutes: 60 * 24 * 365 },
  { key: "months", minutes: 60 * 24 * 30 },
  { key: "weeks", minutes: 60 * 24 * 7 },
  { key: "days", minutes: 60 * 24 },
  { key: "hours", minutes: 60 },
  { key: "minutes", minutes: 1 },
  { key: "never", minutes: null },
] as const;

export type ShareLinkDurationUnit =
  (typeof shareLinkDurationUnits)[number]["key"];

export const maximumShareLinkMinutes = Math.max(
  ...shareLinkExpirationPresets.map((preset) => preset.minutes ?? 0),
);

export const getShareLinkDuration = (minutes: number | null) => {
  if (minutes === null) {
    return { value: "", unit: "never" as const };
  }

  const unit = shareLinkDurationUnits.find(
    (option) => option.minutes !== null && minutes % option.minutes === 0,
  );
  if (!unit || unit.minutes === null) {
    throw new Error("Unsupported share link duration");
  }
  return { value: String(minutes / unit.minutes), unit: unit.key };
};

export const getShareLinkExpirationMinutes = (
  value: string,
  unit: ShareLinkDurationUnit,
): number | null | undefined => {
  const option = shareLinkDurationUnits.find((entry) => entry.key === unit);
  if (!option) {
    throw new Error("Unsupported share link duration unit");
  }
  if (option.minutes === null) {
    return null;
  }

  const minutes = Number(value.trim()) * option.minutes;
  return Number.isInteger(minutes) &&
    minutes > 0 &&
    minutes <= maximumShareLinkMinutes
    ? minutes
    : undefined;
};

export const isValidShareLinkName = (name: string): boolean =>
  name.trim().length === 0 || /^[\p{L}\p{N}_-]+$/u.test(name.trim());
