export type ClientDiagnosticSource =
  | "app.error"
  | "app.warning"
  | "app.debug"
  | "window.error"
  | "unhandledrejection"
  | "resource.error";

export type ClientDiagnostic = {
  timestamp: string;
  source: ClientDiagnosticSource;
  message: string;
};

const MAX_DIAGNOSTICS = 30;
const diagnostics: ClientDiagnostic[] = [];

export const toDiagnosticMessage = <T>(value: T): string => {
  if (value instanceof Error) {
    const stack = value.stack ? `\n${value.stack}` : "";
    return `${value.name}: ${value.message}${stack}`;
  }
  if (typeof value === "string") {
    return value;
  }
  try {
    return JSON.stringify(value) ?? String(value);
  } catch {
    return String(value);
  }
};

export const recordClientDiagnostic = (
  source: ClientDiagnosticSource,
  message: string,
): void => {
  diagnostics.push({ timestamp: new Date().toISOString(), source, message });
  if (diagnostics.length > MAX_DIAGNOSTICS) {
    diagnostics.splice(0, diagnostics.length - MAX_DIAGNOSTICS);
  }
};

export const getRecentClientDiagnostics = (): ClientDiagnostic[] =>
  diagnostics.slice(-15);

export const reportClientError = <T, U>(
  message: string,
  detail?: T,
  context?: U,
): void => {
  const parts = [message];
  if (detail !== undefined) {
    parts.push(toDiagnosticMessage(detail));
  }
  if (context !== undefined) {
    parts.push(toDiagnosticMessage(context));
  }
  recordClientDiagnostic("app.error", parts.join(" "));
};

export const reportClientWarning = <T>(message: string, detail?: T): void => {
  recordClientDiagnostic(
    "app.warning",
    detail === undefined
      ? message
      : `${message} ${toDiagnosticMessage(detail)}`,
  );
};

export const reportClientDebug = <T>(message: string, detail?: T): void => {
  recordClientDiagnostic(
    "app.debug",
    detail === undefined
      ? message
      : `${message} ${toDiagnosticMessage(detail)}`,
  );
};
