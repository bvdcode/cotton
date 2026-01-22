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

export const formatDuration = (duration: string): string => {
  // Parse C# TimeSpan format (e.g., "01:23:45" or "1.02:03:04")
  const parts = duration.split(".");
  const timePart = parts.length > 1 ? parts[1] : parts[0];
  const [hours, minutes] = timePart.split(":");

  const totalHours =
    parts.length > 1
      ? parseInt(parts[0]) * 24 + parseInt(hours)
      : parseInt(hours);

  if (totalHours < 1) {
    return `${parseInt(minutes)}m`;
  } else if (totalHours < 24) {
    return `${totalHours}h`;
  } else {
    const days = Math.floor(totalHours / 24);
    return `${days}d`;
  }
};

export const getDeviceIcon = (device: string) => {
  const deviceLower = device.toLowerCase();

  if (deviceLower.includes("iphone") || deviceLower.includes("ipod")) {
    return <PhoneIphoneIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("ipad")) {
    return <TabletIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("android phone")) {
    return <PhoneAndroidIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("android tablet")) {
    return <TabletIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (
    deviceLower.includes("windows pc") ||
    deviceLower.includes("mac") ||
    deviceLower.includes("linux pc")
  ) {
    return <ComputerIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("chromebook")) {
    return <LaptopIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("smart tv")) {
    return <TvIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("game console")) {
    return <SportsEsportsIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("bot")) {
    return <SmartToyIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("script")) {
    return <CodeIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("server")) {
    return <DnsIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }
  if (deviceLower.includes("mobile")) {
    return <PhoneIphoneIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
  }

  return <DevicesIcon sx={{ color: "text.secondary", fontSize: 28 }} />;
};
