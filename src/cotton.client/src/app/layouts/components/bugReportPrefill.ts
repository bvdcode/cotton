import { UserRole } from "../../../features/auth";
import {
  getRecentClientDiagnostics,
  recordClientDiagnostic,
  toDiagnosticMessage,
} from "../../../shared/utils/clientDiagnostics";

type BrowserDetails = {
  name: string;
  version: string;
};

export type BuildBugReportUrlArgs = {
  serverVersion?: string | null;
  userRole?: number | null;
  currentUrl: string;
};

const ISSUE_URL = "https://github.com/bvdcode/cotton/issues/new";
const MAX_CONSOLE_BLOCK_LENGTH = 8000;
const MAX_SINGLE_ERROR_LENGTH = 1200;
let captureInstalled = false;

const truncateText = (value: string, maxLength: number): string =>
  value.length <= maxLength
    ? value
    : `${value.slice(0, maxLength)}\n...[truncated]`;

const sanitizeForCodeBlock = (value: string): string =>
  value.replaceAll("```", "'''");

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

const maskCurrentUrlHost = (href: string): string => {
  try {
    const parsed = new URL(href);
    return `${parsed.protocol}//<redacted-host>${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return "<unavailable>";
  }
};

const getRoleLabel = (role: number | null | undefined): string => {
  if (role === UserRole.Admin) {
    return "Admin";
  }
  if (role === UserRole.User) {
    return "User";
  }
  return "Unknown";
};

const buildConsoleErrorsMarkdown = (): string | null => {
  const entries = getRecentClientDiagnostics();
  if (entries.length === 0) {
    return null;
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

export const initializeBugReportDiagnostics = (): void => {
  if (captureInstalled) {
    return;
  }

  captureInstalled = true;

  window.addEventListener("error", (event: ErrorEvent) => {
    const location = event.filename
      ? ` @ ${event.filename}:${event.lineno}:${event.colno}`
      : "";
    const errorDetails =
      event.error instanceof Error
        ? toDiagnosticMessage(event.error)
        : event.message || "Unknown window error";

    recordClientDiagnostic("window.error", `${errorDetails}${location}`);
  });

  // Capture resource load errors (scripts/styles/images) that do not bubble
  // and often appear in DevTools as "Failed to load resource".
  window.addEventListener(
    "error",
    (event: Event) => {
      if (event instanceof ErrorEvent) {
        return;
      }

      const target = event.target as
        | (EventTarget & { src?: string; href?: string; tagName?: string })
        | null;
      if (!target) {
        return;
      }

      const src = typeof target.src === "string" ? target.src : "";
      const href = typeof target.href === "string" ? target.href : "";
      const resourceUrl = src || href;
      if (!resourceUrl) {
        return;
      }

      const tagName =
        typeof target.tagName === "string" && target.tagName.length > 0
          ? target.tagName
          : "resource";

      recordClientDiagnostic(
        "resource.error",
        `${tagName} failed to load: ${resourceUrl}`,
      );
    },
    true,
  );

  window.addEventListener(
    "unhandledrejection",
    (event: PromiseRejectionEvent) => {
      recordClientDiagnostic(
        "unhandledrejection",
        toDiagnosticMessage(event.reason),
      );
    },
  );
};

export const buildBugReportUrl = ({
  serverVersion,
  userRole,
  currentUrl,
}: BuildBugReportUrlArgs): string => {
  const version = serverVersion ?? "unknown";
  const browser = detectBrowser();
  const os = detectOs();
  const role = getRoleLabel(userRole);
  const openedAtUtc = new Date().toISOString();
  const consoleErrorsMarkdown = buildConsoleErrorsMarkdown();
  const consoleErrorsSection = consoleErrorsMarkdown
    ? `
## Client diagnostics (recent)
${consoleErrorsMarkdown}
`
    : "";

  const url = new URL(ISSUE_URL);
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
- Current URL: ${maskCurrentUrlHost(currentUrl)}
- Opened at (UTC): ${openedAtUtc}
${consoleErrorsSection}
`,
  );

  return url.toString();
};
