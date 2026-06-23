import { mdiGooglePlay, mdiLinux, mdiMicrosoftWindows } from "@mdi/js";
import AndroidIcon from "@mui/icons-material/Android";
import { Box, Tooltip } from "@mui/material";
import SvgIcon, { type SvgIconProps } from "@mui/material/SvgIcon";

interface PathIconProps extends SvgIconProps {
  path: string;
}

const PathIcon = ({ path, ...props }: PathIconProps) => (
  <SvgIcon {...props}>
    <path d={path} />
  </SvgIcon>
);

const appDownloadLinks = [
  {
    label: "Google Play testing",
    url: "https://play.google.com/apps/testing/dev.cottoncloud.app",
    icon: <PathIcon fontSize="small" path={mdiGooglePlay} />,
  },
  {
    label: "Android APK",
    url: "https://github.com/bvdcode/cotton-mobile/releases/latest/download/CottonCloud-Android.apk",
    icon: <AndroidIcon fontSize="small" />,
  },
  {
    label: "Windows installer",
    url: "https://github.com/bvdcode/cotton-sync-client/releases/latest/download/CottonSync-Windows-Setup.exe",
    icon: <PathIcon fontSize="small" path={mdiMicrosoftWindows} />,
  },
  {
    label: "Linux DEB package",
    url: "https://github.com/bvdcode/cotton-sync-client/releases/latest/download/CottonSync-Linux.deb",
    icon: <PathIcon fontSize="small" path={mdiLinux} />,
  },
] as const;

interface UserMenuAppDownloadsProps {
  onOpenLink: () => void;
}

export const UserMenuAppDownloads = ({
  onOpenLink,
}: UserMenuAppDownloadsProps) => (
  <Box
    aria-label="Application downloads"
    sx={{
      display: "flex",
      justifyContent: "center",
      gap: 0.25,
    }}
  >
    {appDownloadLinks.map((link) => (
      <Tooltip key={link.url} title={link.label} arrow>
        <Box
          component="a"
          href={link.url}
          target="_blank"
          rel="noopener noreferrer"
          aria-label={link.label}
          onClick={onOpenLink}
          sx={{
            width: 28,
            height: 28,
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            borderRadius: 0.75,
            color: "text.secondary",
            textDecoration: "none",
            transition: (theme) =>
              theme.transitions.create(["outline-color", "color"], {
                duration: theme.transitions.duration.shorter,
              }),
            "&:hover": {
              color: "primary.main",
            },
            "&:focus-visible": {
              outline: "2px solid",
              outlineColor: "primary.main",
              outlineOffset: 2,
            },
          }}
        >
          {link.icon}
        </Box>
      </Tooltip>
    ))}
  </Box>
);
