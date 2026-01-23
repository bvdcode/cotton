import PhoneIphoneIcon from "@mui/icons-material/PhoneIphone";
import PhoneAndroidIcon from "@mui/icons-material/PhoneAndroid";
import TabletIcon from "@mui/icons-material/Tablet";
import ComputerIcon from "@mui/icons-material/Computer";
import LaptopIcon from "@mui/icons-material/Laptop";
import TvIcon from "@mui/icons-material/Tv";
import SportsEsportsIcon from "@mui/icons-material/SportsEsports";
import SmartToyIcon from "@mui/icons-material/SmartToy";
import CodeIcon from "@mui/icons-material/Code";
import DnsIcon from "@mui/icons-material/Dns";
import DevicesIcon from "@mui/icons-material/Devices";
import type { SessionDto } from "../../../shared/api/sessionsApi";

export const formatLocation = (session: SessionDto): string => {
  const parts = [session.city, session.region, session.country].filter(Boolean);
  return parts.join(", ") || "Unknown location";
};

import type { TFunction } from "i18next";

export const formatDuration = (duration: string, t: TFunction): string => {
  // Parse C# TimeSpan format: "hh:mm:ss", "d.hh:mm:ss", or "d.hh:mm:ss.fffffff"
  // Examples: "01:23:45", "1.02:03:04", "365.00:00:00.0000000"
  
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

  const totalHours = days * 24 + hours;
  const totalMinutes = totalHours * 60 + minutes;

  if (totalMinutes === 0) {
    return t("common:time.lessThanMinute");
  } else if (totalMinutes < 60) {
    return t("common:time.durationMinutes", { count: totalMinutes });
  } else if (totalHours < 24) {
    return t("common:time.durationHours", { count: totalHours });
  } else {
    return t("common:time.durationDays", { count: days || Math.floor(totalHours / 24) });
  }
};

export const getDeviceIcon = (device: string) => {
  const deviceLower = (device ?? "").toLowerCase();
  const fontSize = 28;
  const color = "text.secondary";
  const styles = { color: color, fontSize: fontSize };

  if (!deviceLower) {
    return <DevicesIcon sx={{ color: color, fontSize: fontSize }} />;
  }

  if (deviceLower.includes("iphone") || deviceLower.includes("ipod")) {
    return <PhoneIphoneIcon sx={styles} />;
  }
  if (deviceLower.includes("ipad")) {
    return <TabletIcon sx={styles} />;
  }
  if (deviceLower.includes("android phone")) {
    return <PhoneAndroidIcon sx={styles} />;
  }
  if (deviceLower.includes("android tablet")) {
    return <TabletIcon sx={styles} />;
  }

  const looksLikeAndroidPhoneByBrand =
    deviceLower.includes("google pixel") ||
    deviceLower.includes("oneplus") ||
    deviceLower.includes("xiaomi") ||
    (deviceLower.startsWith("samsung") &&
      !deviceLower.includes("watch") &&
      !deviceLower.includes("tablet"));

  if (looksLikeAndroidPhoneByBrand) {
    return <PhoneAndroidIcon sx={styles} />;
  }

  if (
    deviceLower.includes("windows pc") ||
    deviceLower.includes("mac") ||
    deviceLower.includes("linux pc")
  ) {
    return <ComputerIcon sx={styles} />;
  }
  if (deviceLower.includes("chromebook")) {
    return <LaptopIcon sx={styles} />;
  }
  if (deviceLower.includes("smart tv")) {
    return <TvIcon sx={styles} />;
  }
  if (deviceLower.includes("game console")) {
    return <SportsEsportsIcon sx={styles} />;
  }
  if (deviceLower.includes("bot")) {
    return <SmartToyIcon sx={styles} />;
  }
  if (deviceLower.includes("script")) {
    return <CodeIcon sx={styles} />;
  }
  if (deviceLower.includes("server")) {
    return <DnsIcon sx={styles} />;
  }
  if (deviceLower.includes("mobile")) {
    return <PhoneIphoneIcon sx={styles} />;
  }

  return <DevicesIcon sx={styles} />;
};
