import {
  Avatar,
  IconButton,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText,
  Divider,
  Typography,
  Box,
} from "@mui/material";
import {
  Logout,
  Person,
  AdminPanelSettings,
  BugReport,
} from "@mui/icons-material";
import { UserRole, useAuth } from "../../../features/auth";
import { useTranslation } from "react-i18next";
import { useEffect, useState, type MouseEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useServerSettings } from "../../../shared/store/useServerSettings";

type ConsoleErrorSource =
  | "console.error"
  | "window.error"
  | "unhandledrejection";

type BrowserDetails = {
  name: string;
  version: string;
};

type ConsoleErrorEntry = {
  timestamp: string;
  source: ConsoleErrorSource;
  message: string;
};

const MAX_CAPTURED_CONSOLE_ERRORS = 30;
const MAX_CONSOLE_BLOCK_LENGTH = 8000;
const MAX_SINGLE_ERROR_LENGTH = 1200;
const RECENT_CONSOLE_ERRORS: ConsoleErrorEntry[] = [];
const CONSOLE_CAPTURE_FLAG = "__cottonConsoleCaptureInstalled";

const truncateText = (value: string, maxLength: number): string =>
  value.length <= maxLength
    ? value
    : `${value.slice(0, maxLength)}\n...[truncated]`;

const sanitizeForCodeBlock = (value: string): string =>
  value.replaceAll("```", "'''");

const toConsoleMessage = (value: unknown): string => {
  if (value instanceof Error) {
    const stack = value.stack ? `\n${value.stack}` : "";
    return `${value.name}: ${value.message}${stack}`;
  }

  if (typeof value === "string") {
    return value;
  }

  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
};

const formatConsoleArgs = (args: unknown[]): string =>
  args.map((arg) => toConsoleMessage(arg)).join(" ");

const pushConsoleError = (
  source: ConsoleErrorSource,
  message: string,
): void => {
  RECENT_CONSOLE_ERRORS.push({
    timestamp: new Date().toISOString(),
    source,
    message,
  });

  if (RECENT_CONSOLE_ERRORS.length > MAX_CAPTURED_CONSOLE_ERRORS) {
    RECENT_CONSOLE_ERRORS.splice(
      0,
      RECENT_CONSOLE_ERRORS.length - MAX_CAPTURED_CONSOLE_ERRORS,
    );
  }
};

const installConsoleErrorCapture = (): void => {
  const bag = window as unknown as Record<string, unknown>;
  if (bag[CONSOLE_CAPTURE_FLAG] === true) {
    return;
  }

  bag[CONSOLE_CAPTURE_FLAG] = true;

  const originalConsoleError = console.error.bind(console);
  console.error = (...args: unknown[]) => {
    pushConsoleError("console.error", formatConsoleArgs(args));
    originalConsoleError(...args);
  };

  window.addEventListener("error", (event: ErrorEvent) => {
    const location = event.filename
      ? ` @ ${event.filename}:${event.lineno}:${event.colno}`
      : "";
    const errorDetails =
      event.error instanceof Error
        ? toConsoleMessage(event.error)
        : event.message || "Unknown window error";

    pushConsoleError("window.error", `${errorDetails}${location}`);
  });

  window.addEventListener(
    "unhandledrejection",
    (event: PromiseRejectionEvent) => {
      pushConsoleError("unhandledrejection", toConsoleMessage(event.reason));
    },
  );
};

const buildConsoleErrorsMarkdown = (): string => {
  const entries = RECENT_CONSOLE_ERRORS.slice(-15);
  if (entries.length === 0) {
    return [
      "_Captured in this tab since page load._",
      "_No recent console errors were captured._",
    ].join("\n");
  }

  const lines = entries
    .map((entry, index) => {
      const message = truncateText(
        sanitizeForCodeBlock(entry.message),
        MAX_SINGLE_ERROR_LENGTH,
      );

      return `#${index + 1} [${entry.timestamp}] ${entry.source}\n${message}`;
    })
    .join("\n\n");

  const content = truncateText(lines, MAX_CONSOLE_BLOCK_LENGTH);

  return [
    "_Captured in this tab since page load._",
    "",
    "> Please review this block before submitting. It may contain personal data.",
    "",
    "```text",
    content,
    "```",
  ].join("\n");
};

const maskCurrentUrlHost = (href: string): string => {
  try {
    const parsed = new URL(href);
    return `${parsed.protocol}//<redacted-host>${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return "<unavailable>";
  }
};

const getRoleLabel = (role: UserRole | null | undefined): string => {
  if (role === UserRole.Admin) {
    return "Admin";
  }

  if (role === UserRole.User) {
    return "User";
  }

  return "Unknown";
};

export const UserMenu = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation("common");
  const { data: serverSettings } = useServerSettings();
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const isOpen = Boolean(anchorEl);

  useEffect(() => {
    installConsoleErrorCapture();
  }, []);

  const getAvatarInitials = (args: {
    firstName?: string | null;
    lastName?: string | null;
    username?: string | null;
    email?: string | null;
    displayName?: string | null;
  }): string => {
    const first = (args.firstName ?? "").trim();
    const last = (args.lastName ?? "").trim();
    if (first && last) {
      return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
    }

    const fallback = (
      args.displayName ??
      args.username ??
      args.email ??
      ""
    ).trim();
    if (!fallback) return "";

    const parts = fallback.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0].charAt(0)}${parts[1].charAt(0)}`.toUpperCase();
    }

    return fallback.slice(0, 2).toUpperCase();
  };

  const handleOpen = (event: MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleLogout = async () => {
    handleClose();
    await logout();
  };

  const detectBrowser = (): BrowserDetails => {
    const ua = navigator.userAgent;

    const resolve = (name: string, versionRegex: RegExp): BrowserDetails => {
      const match = ua.match(versionRegex);
      return {
        name,
        version: match?.[1] ?? "unknown",
      };
    };

    if (ua.includes("Edg/")) {
      return resolve("Microsoft Edge", /Edg\/([0-9.]+)/);
    }
    if (ua.includes("OPR/") || ua.includes("Opera")) {
      return resolve("Opera", /(?:OPR|Opera)\/([0-9.]+)/);
    }
    if (ua.includes("Firefox/")) {
      return resolve("Firefox", /Firefox\/([0-9.]+)/);
    }
    if (ua.includes("Chrome/")) {
      return resolve("Chrome", /Chrome\/([0-9.]+)/);
    }
    if (ua.includes("Safari/")) {
      return resolve("Safari", /Version\/([0-9.]+)/);
    }

    return {
      name: "Unknown",
      version: "unknown",
    };
  };

  const detectOs = (): string => {
    const ua = navigator.userAgent;

    if (ua.includes("Windows NT")) {
      return "Windows";
    }
    if (ua.includes("Mac OS X")) {
      return "macOS";
    }
    if (ua.includes("Android")) {
      return "Android";
    }
    if (ua.includes("iPhone") || ua.includes("iPad")) {
      return "iOS";
    }
    if (ua.includes("Linux")) {
      return "Linux";
    }

    return "Unknown";
  };

  const openBugReport = () => {
    handleClose();

    const version = serverSettings?.version ?? "unknown";
    const browser = detectBrowser();
    const os = detectOs();
    const role = getRoleLabel(user?.role);
    const currentUrl = maskCurrentUrlHost(window.location.href);
    const openedAtUtc = new Date().toISOString();
    const consoleErrorsMarkdown = buildConsoleErrorsMarkdown();

    const url = new URL("https://github.com/bvdcode/cotton/issues/new");
    url.searchParams.set("labels", "bug");
    url.searchParams.set("assignees", "bvdcode");
    url.searchParams.set("title", "[Bug]: ");
    url.searchParams.set(
      "body",
      `## Version
${version}

## Description


## Steps to reproduce
1. 
2. 
3. 

## Expected behavior


## Actual behavior


## Environment
- Cotton version: ${version}
- Browser: ${browser.name}
- Browser version: ${browser.version}
- OS: ${os}
- User role: ${role}
- Current URL: ${currentUrl}
- Opened at (UTC): ${openedAtUtc}

## Console errors (recent)
${consoleErrorsMarkdown}
`,
    );

    window.open(url.toString(), "_blank", "noopener,noreferrer");
  };

  const fullName = [user?.firstName, user?.lastName]
    .filter(Boolean)
    .join(" ")
    .trim();
  const displayName =
    fullName ||
    user?.displayName ||
    user?.username ||
    user?.email ||
    t("userMenu.user");
  const caption = user?.username ? `@${user.username}` : user?.email || "";
  const avatarInitials = getAvatarInitials({
    firstName: user?.firstName,
    lastName: user?.lastName,
    username: user?.username,
    email: user?.email,
    displayName,
  });
  const isAdmin = user?.role === UserRole.Admin;

  return (
    <>
      <IconButton
        onClick={handleOpen}
        size="small"
        aria-controls={isOpen ? "user-menu" : undefined}
        aria-haspopup="true"
        aria-expanded={isOpen ? "true" : undefined}
        sx={{
          padding: 0,
        }}
      >
        <Avatar
          alt={displayName}
          src={user?.pictureUrl}
          sx={{
            width: 40,
            height: 40,
            bgcolor: "primary.main",
          }}
        >
          {!user?.pictureUrl && avatarInitials}
        </Avatar>
      </IconButton>

      <Menu
        id="user-menu"
        anchorEl={anchorEl}
        open={isOpen}
        onClose={handleClose}
        transformOrigin={{ horizontal: "right", vertical: "top" }}
        anchorOrigin={{ horizontal: "right", vertical: "bottom" }}
        slotProps={{
          paper: {
            elevation: 3,
            sx: {
              mt: 1.5,
              minWidth: 200,
            },
          },
        }}
      >
        <Box px={2} py={1.5}>
          <Typography variant="subtitle2" noWrap>
            {displayName}
          </Typography>
          {caption && caption !== displayName && (
            <Typography variant="body2" color="text.primary" noWrap>
              {caption}
            </Typography>
          )}
        </Box>

        <Divider />

        <MenuItem
          onClick={() => {
            handleClose();
            navigate("/settings");
          }}
        >
          <ListItemIcon>
            <Person fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.profile")}</ListItemText>
        </MenuItem>

        {isAdmin && (
          <MenuItem
            onClick={() => {
              handleClose();
              navigate("/admin/users");
            }}
          >
            <ListItemIcon>
              <AdminPanelSettings fontSize="small" />
            </ListItemIcon>
            <ListItemText>{t("userMenu.admin")}</ListItemText>
          </MenuItem>
        )}

        <MenuItem
          onClick={() => {
            openBugReport();
          }}
        >
          <ListItemIcon>
            <BugReport fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.help")}</ListItemText>
        </MenuItem>

        <MenuItem onClick={handleLogout}>
          <ListItemIcon>
            <Logout fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.logout")}</ListItemText>
        </MenuItem>

        {serverSettings?.version && <Divider />}
        {serverSettings?.version && (
          <Box px={2}>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: "block", textAlign: "center" }}
            >
              {serverSettings.version}
            </Typography>
          </Box>
        )}
      </Menu>
    </>
  );
};
