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
 * Device type matchers
 */
const deviceMatchers = {
  // Apple devices
  iPhone: (device: string) =>
    device.includes("iphone") || device.includes("ipod"),
  iPad: (device: string) => device.includes("ipad"),

  // Android devices
  androidPhone: (device: string) => device.includes("android phone"),
  androidTablet: (device: string) => device.includes("android tablet"),

  // Desktop/Laptop
  desktop: (device: string) =>
    device.includes("windows pc") ||
    device.includes("mac") ||
    device.includes("linux pc"),
  chromebook: (device: string) => device.includes("chromebook"),

  // Other devices
  smartTV: (device: string) => device.includes("smart tv"),
  gameConsole: (device: string) => device.includes("game console"),
  bot: (device: string) => device.includes("bot"),
  script: (device: string) => device.includes("script"),
  server: (device: string) => device.includes("server"),
  mobile: (device: string) => device.includes("mobile"),
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
  if (deviceMatchers.iPhone(deviceLower))
    return <PhoneIphoneIcon sx={ICON_STYLES} />;
  if (deviceMatchers.iPad(deviceLower)) return <TabletIcon sx={ICON_STYLES} />;

  // Android devices
  if (deviceMatchers.androidPhone(deviceLower))
    return <PhoneAndroidIcon sx={ICON_STYLES} />;
  if (deviceMatchers.androidTablet(deviceLower))
    return <TabletIcon sx={ICON_STYLES} />;
  if (isAndroidPhoneBrand(deviceLower))
    return <PhoneAndroidIcon sx={ICON_STYLES} />;

  // Desktop/laptop devices
  if (deviceMatchers.desktop(deviceLower))
    return <ComputerIcon sx={ICON_STYLES} />;
  if (deviceMatchers.chromebook(deviceLower))
    return <LaptopIcon sx={ICON_STYLES} />;

  // Other device types
  if (deviceMatchers.smartTV(deviceLower)) return <TvIcon sx={ICON_STYLES} />;
  if (deviceMatchers.gameConsole(deviceLower))
    return <SportsEsportsIcon sx={ICON_STYLES} />;
  if (deviceMatchers.bot(deviceLower)) return <SmartToyIcon sx={ICON_STYLES} />;
  if (deviceMatchers.script(deviceLower))
    return <CodeIcon sx={ICON_STYLES} />;
  if (deviceMatchers.server(deviceLower)) return <DnsIcon sx={ICON_STYLES} />;
  if (deviceMatchers.mobile(deviceLower))
    return <PhoneIphoneIcon sx={ICON_STYLES} />;

  return <DevicesIcon sx={ICON_STYLES} />;
};
