import type { TFunction } from "i18next";

export interface TimeAgoResult {
  key: string;
  count: number;
}

/**
 * Calculates relative time and returns i18n key with count for proper localization
 */
export const getTimeAgo = (isoDate: string): TimeAgoResult => {
  const date = new Date(isoDate);
  const now = new Date();
  const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);

  if (seconds < 10) {
    return { key: "common:time.justNow", count: 0 };
  }

  if (seconds < 60) {
    return { key: "common:time.secondsAgo", count: seconds };
  }

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return { key: "common:time.minutesAgo", count: minutes };
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return { key: "common:time.hoursAgo", count: hours };
  }

  const days = Math.floor(hours / 24);
  if (days < 30) {
    return { key: "common:time.daysAgo", count: days };
  }

  const months = Math.floor(days / 30);
  if (months < 12) {
    return { key: "common:time.monthsAgo", count: months };
  }

  const years = Math.floor(months / 12);
  return { key: "common:time.yearsAgo", count: years };
};

/**
 * Formats a date/time as a relative time string using i18n
 */
export const formatTimeAgo = (isoDate: string, t: TFunction): string => {
  const { key, count } = getTimeAgo(isoDate);
  return t(key, { count });
};
