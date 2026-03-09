import type { AxiosResponseHeaders, RawAxiosResponseHeaders } from "axios";

type HeaderPrimitive = string | number | boolean | null | undefined;

type HeaderMap = RawAxiosResponseHeaders & AxiosResponseHeaders;

type GettableHeaders = {
  get?: (name: string) => HeaderPrimitive;
};

const tryReadHeader = (
  headers: HeaderMap,
  name: string,
): HeaderPrimitive => {
  const direct = headers[name];
  if (direct !== undefined && direct !== null) {
    return direct;
  }

  const lower = headers[name.toLowerCase()];
  if (lower !== undefined && lower !== null) {
    return lower;
  }

  const getter = (headers as HeaderMap & GettableHeaders).get;
  if (typeof getter === "function") {
    const fromGet = getter(name) ?? getter(name.toLowerCase());
    if (fromGet !== undefined && fromGet !== null) {
      return fromGet;
    }
  }

  return undefined;
};

export const readRequiredIntHeader = (
  headers: HeaderMap,
  headerName: string,
): number => {
  const value = tryReadHeader(headers, headerName);
  const parsed = Number.parseInt(String(value ?? ""), 10);

  if (!Number.isFinite(parsed)) {
    throw new Error(`${headerName} header is missing or invalid`);
  }

  return parsed;
};
