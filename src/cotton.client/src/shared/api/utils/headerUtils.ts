import { isRecord } from "../../utils/typeGuards";

type HeaderPrimitive = string | number | boolean | string[] | null | undefined;

const tryReadHeader = (headers: unknown, name: string): HeaderPrimitive => {
  if (!isRecord(headers)) return undefined;
  const direct = headers[name];
  if (
    typeof direct === "string" ||
    typeof direct === "number" ||
    typeof direct === "boolean" ||
    Array.isArray(direct) ||
    direct === null
  ) {
    return direct;
  }

  const lower = headers[name.toLowerCase()];
  if (
    typeof lower === "string" ||
    typeof lower === "number" ||
    typeof lower === "boolean" ||
    Array.isArray(lower) ||
    lower === null
  ) {
    return lower;
  }

  return undefined;
};

export const readRequiredIntHeader = (
  headers: unknown,
  headerName: string,
): number => {
  const value = tryReadHeader(headers, headerName);
  const parsed = Number.parseInt(String(value ?? ""), 10);

  if (!Number.isFinite(parsed)) {
    throw new Error(`${headerName} header is missing or invalid`);
  }

  return parsed;
};
