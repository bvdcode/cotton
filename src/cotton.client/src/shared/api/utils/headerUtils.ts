import type { AxiosResponseHeaders, RawAxiosResponseHeaders } from "axios";

type HeaderPrimitive = string | number | boolean | string[] | null | undefined;

const tryReadHeader = (
  headers: AxiosResponseHeaders | RawAxiosResponseHeaders,
  name: string,
): HeaderPrimitive => {
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
  headers: AxiosResponseHeaders | RawAxiosResponseHeaders,
  headerName: string,
): number => {
  const value = tryReadHeader(headers, headerName);
  const parsed = Number.parseInt(String(value ?? ""), 10);

  if (!Number.isFinite(parsed)) {
    throw new Error(`${headerName} header is missing or invalid`);
  }

  return parsed;
};
