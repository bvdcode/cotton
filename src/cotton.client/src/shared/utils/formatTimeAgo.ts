import i18n from "../../i18n";

/**
 * Formats a date/time as a relative time string (e.g., "2 minutes ago", "Just now")
 */
export const formatTimeAgo = (isoDate: string): string => {
  const date = new Date(isoDate);
  const now = new Date();
  const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);

  if (seconds < 10) {
    return i18n.t("common.time.justNow");
  }

  if (seconds < 60) {
    return i18n.t("common.time.secondsAgo", { count: seconds });
  }

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return i18n.t("common.time.minutesAgo", { count: minutes });
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return i18n.t("common.time.hoursAgo", { count: hours });
  }

  const days = Math.floor(hours / 24);
  if (days < 30) {
    return i18n.t("common.time.daysAgo", { count: days });
  }

  const months = Math.floor(days / 30);
  if (months < 12) {
    return i18n.t("common.time.monthsAgo", { count: months });
  }

  const years = Math.floor(months / 12);
  return i18n.t("common.time.yearsAgo", { count: years });
};
