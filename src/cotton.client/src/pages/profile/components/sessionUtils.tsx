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
  // Check if there's a day component (contains a dot before the time part)
  const dayMatch = duration.match(/^(\d+)\.(\d{2}:\d{2}:\d{2})/);

  if (dayMatch) {
    const timePart = dayMatch[2];
    const [hours, minutes] = timePart.split(":");

    return {
      days: parseInt(dayMatch[1], 10),
      hours: parseInt(hours, 10),
      minutes: parseInt(minutes, 10),
    };
  }

  // No day component, just time
  const timePart = duration.split(".")[0]; // Remove fractional seconds if present
  const [hours, minutes] = timePart.split(":");

  return {
    days: 0,
    hours: parseInt(hours, 10) || 0,
    minutes: parseInt(minutes, 10) || 0,
  };
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
