import { UserRole } from "../../../features/auth";

type ConsoleErrorSource =
  | "console.error"
  | "window.error"
  | "unhandledrejection"
  | "resource.error";

type BrowserDetails = {
  name: string;
  version: string;
  userAgent: string;
};

type ConsoleErrorEntry = {
  timestamp: string;
  source: ConsoleErrorSource;
  message: string;
};

type ConsoleCaptureState = {
  installed: boolean;
  errors: ConsoleErrorEntry[];
  originalConsoleError?: typeof console.error;
};

export type BuildBugReportUrlArgs = {
  serverVersion?: string | null;
  userRole?: number | null;
  currentUrl: string;
};

const ISSUE_URL = "https://github.com/bvdcode/cotton/issues/new";
const MAX_CAPTURED_CONSOLE_ERRORS = 30;
const MAX_CONSOLE_BLOCK_LENGTH = 8000;
const MAX_SINGLE_ERROR_LENGTH = 1200;
const CAPTURE_STATE_KEY = "__cottonBugReportConsoleCaptureState";

const getConsoleCaptureState = (): ConsoleCaptureState => {
  const bag = window as unknown as Record<string, unknown>;
  const existing = bag[CAPTURE_STATE_KEY] as ConsoleCaptureState | undefined;
  if (
    existing &&
    typeof existing === "object" &&
    Array.isArray(existing.errors)
  ) {
    return existing;
  }

  const created: ConsoleCaptureState = {
    installed: false,
    errors: [],
  };
  bag[CAPTURE_STATE_KEY] = created;
  return created;
};

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

const pushConsoleError = (source: ConsoleErrorSource, message: string): void => {
  const state = getConsoleCaptureState();
  state.errors.push({
    timestamp: new Date().toISOString(),
    source,
    message,
  });

  if (state.errors.length > MAX_CAPTURED_CONSOLE_ERRORS) {
    state.errors.splice(
      0,
      state.errors.length - MAX_CAPTURED_CONSOLE_ERRORS,
    );
  }
};

const detectBrowser = (): BrowserDetails => {
  const ua = navigator.userAgent;

  const resolve = (name: string, versionRegex: RegExp): BrowserDetails => {
    const match = ua.match(versionRegex);
    return {
      name,
      version: match?.[1] ?? "unknown",
      userAgent: ua,
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
    userAgent: ua,
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
  const entries = getConsoleCaptureState().errors.slice(-15);
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

export const initializeBugReportConsoleCapture = (): void => {
  const state = getConsoleCaptureState();
  if (state.installed) {
    return;
  }

  state.installed = true;

  if (!state.originalConsoleError) {
    state.originalConsoleError = console.error.bind(console);
  }
  const originalConsoleError = state.originalConsoleError;
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

      pushConsoleError(
        "resource.error",
        `${tagName} failed to load: ${resourceUrl}`,
      );
    },
    true,
  );

  window.addEventListener(
    "unhandledrejection",
    (event: PromiseRejectionEvent) => {
      pushConsoleError("unhandledrejection", toConsoleMessage(event.reason));
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
## Console errors (recent)
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
- User-Agent: ${browser.userAgent}
- OS: ${os}
- User role: ${role}
- Current URL: ${maskCurrentUrlHost(currentUrl)}
- Opened at (UTC): ${openedAtUtc}
${consoleErrorsSection}
`,
  );

  return url.toString();
};
