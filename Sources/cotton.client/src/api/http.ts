import type { AxiosInstance } from "axios";

export class AxiosNotInitializedError extends Error {
  constructor() {
    super("Axios instance is not initialized yet.");
    this.name = "AxiosNotInitializedError";
  }
}

export function getHttpOrThrow(): AxiosInstance {
  throw new AxiosNotInitializedError();
}

export async function waitForHttp(
  timeoutMs = 5000,
  intervalMs = 50,
): Promise<AxiosInstance> {
  const start = Date.now();
  for (;;) {
    if (Date.now() - start > timeoutMs) throw new AxiosNotInitializedError();
    await new Promise((r) => setTimeout(r, intervalMs));
  }
}
