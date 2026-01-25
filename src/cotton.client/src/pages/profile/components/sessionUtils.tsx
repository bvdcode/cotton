import type { SessionDto } from "../../../shared/api/sessionsApi";
import type { TFunction } from "i18next";

export const formatLocation = (session: SessionDto): string => {
  const parts = [session.city, session.region, session.country].filter(Boolean);
  return parts.join(", ") || "Unknown location";
};

/**
 * Parse time components from C# TimeSpan format
 */
interface TimeComponents {
  days: number;
  hours: number;
  minutes: number;
}

const parseTimeSpan = (duration: string): TimeComponents => {
  let days = 0;
  let hours = 0;
  let minutes = 0;

  // Check if there's a day component (contains a dot before the time part)
  const dayMatch = duration.match(/^(\d+)\.(\d{2}:\d{2}:\d{2})/);

  if (dayMatch) {
    days = parseInt(dayMatch[1], 10);
    const timePart = dayMatch[2];
    const [h, m] = timePart.split(":");
    hours = parseInt(h, 10);
    minutes = parseInt(m, 10);
  } else {
    // No day component, just time
    const timePart = duration.split(".")[0]; // Remove fractional seconds if present
    const [h, m] = timePart.split(":");
    hours = parseInt(h, 10) || 0;
    minutes = parseInt(m, 10) || 0;
  }

  return { days, hours, minutes };
};

/**
 * Format duration string based on total minutes
 */
const formatDurationByMinutes = (
  totalMinutes: number,
  totalHours: number,
  days: number,
  t: TFunction,
): string => {
  if (totalMinutes === 0) {
    return t("common:time.lessThanMinute");
  }
  if (totalMinutes < 60) {
    return t("common:time.durationMinutes", { count: totalMinutes });
  }
  if (totalHours < 24) {
    return t("common:time.durationHours", { count: totalHours });
  }
  return t("common:time.durationDays", {
    count: days || Math.floor(totalHours / 24),
  });
};

/**
 * Format C# TimeSpan duration to human-readable string
 * Supports formats: "hh:mm:ss", "d.hh:mm:ss", or "d.hh:mm:ss.fffffff"
 */
export const formatDuration = (duration: string, t: TFunction): string => {
  const { days, hours, minutes } = parseTimeSpan(duration);
  const totalHours = days * 24 + hours;
  const totalMinutes = totalHours * 60 + minutes;

  return formatDurationByMinutes(totalMinutes, totalHours, days, t);
};

export { getDeviceIcon } from "./deviceIcons";
