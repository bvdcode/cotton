export type ToastSeverity = "error" | "success";

export type TwoFactorServerHint = "required" | "invalid" | "locked";

export function normalizeTwoFactorCode(value: string): string {
  return value.replace(/\D/g, "").slice(0, 6);
}

export function isEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}

export function tryGetTwoFactorHint(args: {
  status: number | undefined;
  serverMessage: string | undefined;
}): TwoFactorServerHint | null {
  const { status, serverMessage } = args;
  if (status !== 403) return null;
  if (typeof serverMessage !== "string") return null;

  const msgLower = serverMessage.toLowerCase();

  if (msgLower.includes("two-factor") && msgLower.includes("required")) {
    return "required";
  }

  if (msgLower.includes("invalid") && msgLower.includes("two-factor")) {
    return "invalid";
  }

  if (
    msgLower.includes("maximum") ||
    msgLower.includes("locked") ||
    msgLower.includes("attempts")
  ) {
    return "locked";
  }

  return null;
}
