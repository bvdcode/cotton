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

const ICON_STYLES = {
  color: "text.secondary",
  fontSize: 28,
};

/**
 * Check if device looks like an Android phone based on brand
 */
const isAndroidPhoneBrand = (deviceLower: string): boolean => {
  return (
    deviceLower.includes("google pixel") ||
    deviceLower.includes("oneplus") ||
    deviceLower.includes("xiaomi") ||
    (deviceLower.startsWith("samsung") &&
      !deviceLower.includes("watch") &&
      !deviceLower.includes("tablet"))
  );
};

/**
 * Get device icon based on device string
 */
export const getDeviceIcon = (device: string) => {
  const deviceLower = (device ?? "").toLowerCase();

  if (!deviceLower) {
    return <DevicesIcon sx={ICON_STYLES} />;
  }

  // Apple devices
  if (deviceLower.includes("iphone") || deviceLower.includes("ipod")) {
    return <PhoneIphoneIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("ipad")) {
    return <TabletIcon sx={ICON_STYLES} />;
  }

  // Android devices
  if (deviceLower.includes("android phone")) {
    return <PhoneAndroidIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("android tablet")) {
    return <TabletIcon sx={ICON_STYLES} />;
  }
  if (isAndroidPhoneBrand(deviceLower)) {
    return <PhoneAndroidIcon sx={ICON_STYLES} />;
  }

  // Desktop/laptop devices
  if (
    deviceLower.includes("windows pc") ||
    deviceLower.includes("mac") ||
    deviceLower.includes("linux pc")
  ) {
    return <ComputerIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("chromebook")) {
    return <LaptopIcon sx={ICON_STYLES} />;
  }

  // Other device types
  if (deviceLower.includes("smart tv")) {
    return <TvIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("game console")) {
    return <SportsEsportsIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("bot")) {
    return <SmartToyIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("script")) {
    return <CodeIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("server")) {
    return <DnsIcon sx={ICON_STYLES} />;
  }
  if (deviceLower.includes("mobile")) {
    return <PhoneIphoneIcon sx={ICON_STYLES} />;
  }

  return <DevicesIcon sx={ICON_STYLES} />;
};
