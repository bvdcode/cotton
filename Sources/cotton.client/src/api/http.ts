import type { AxiosInstance } from "axios";
import { getAxiosInstance } from "@bvdcode/react-kit";

export class AxiosNotInitializedError extends Error {
  constructor() {
    super(
      "Axios instance is not initialized yet. Ensure AppShell is mounted before calling the API or guard your calls to wait for initialization.",
    );
    this.name = "AxiosNotInitializedError";
  }
}

/**
 * Returns the configured Axios instance from AppShell or throws if not initialized yet.
 * Prefer calling this lazily inside API methods to always use the latest tokens/interceptors.
 */
export function getHttpOrThrow(): AxiosInstance {
  const axios = getAxiosInstance();
  if (!axios) {
    throw new AxiosNotInitializedError();
  }
  return axios;
}

/** Optional helper for future: wait until axios is available (polling). */
export async function waitForHttp(
  timeoutMs = 5000,
  intervalMs = 50,
): Promise<AxiosInstance> {
  const start = Date.now();
  for (;;) {
    const axios = getAxiosInstance();
    if (axios) return axios;
    if (Date.now() - start > timeoutMs) throw new AxiosNotInitializedError();
    await new Promise((r) => setTimeout(r, intervalMs));
  }
}
