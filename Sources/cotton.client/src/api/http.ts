import type { AxiosInstance } from "axios";
import { getAxiosInstance } from "@bvdcode/react-kit";

export class AxiosNotInitializedError extends Error {
  constructor() {
    super("Axios instance is not initialized yet.");
    this.name = "AxiosNotInitializedError";
  }
}

export function getHttpOrThrow(): AxiosInstance {
  const axios = getAxiosInstance();
  if (!axios) {
    throw new AxiosNotInitializedError();
  }
  return axios;
}

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
