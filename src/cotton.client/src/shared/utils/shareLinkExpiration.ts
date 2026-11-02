import type { TFunction } from "i18next";

const MINUTES_IN_DAY = 60 * 24;

export const shareLinkExpirationPresets = [
  { key: "oneDay", minutes: MINUTES_IN_DAY },
  { key: "week", minutes: MINUTES_IN_DAY * 7 },
  { key: "month", minutes: MINUTES_IN_DAY * 30 },
  { key: "threeMonths", minutes: MINUTES_IN_DAY * 90 },
  { key: "year", minutes: MINUTES_IN_DAY * 365 },
  { key: "threeYears", minutes: MINUTES_IN_DAY * 365 * 3 },
  { key: "tenYears", minutes: MINUTES_IN_DAY * 365 * 10 },
  { key: "never", minutes: null },
] as const;

export const formatShareLinkExpiration = (
  minutes: number | null,
  t: TFunction,
): string => {
  const preset = shareLinkExpirationPresets.find((p) => p.minutes === minutes);
  if (preset) return t(`profile:shareLinks.presets.${preset.key}`);
  return t("common:time.durationMinutes", { count: minutes ?? 0 });
};
