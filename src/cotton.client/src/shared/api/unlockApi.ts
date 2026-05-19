export interface UnlockStatusResponse {
  requiresBootstrapToken: boolean;
  firstUnlockExpiresAtUtc?: string | null;
}

export interface UnlockResponse {
  ok: boolean;
  message: string;
}

const jsonHeaders = {
  Accept: "application/json",
  "Content-Type": "application/json",
};

const acceptsJson = (response: Response): boolean =>
  response.headers.get("content-type")?.toLowerCase().includes("application/json") ??
  false;

const isUnlockStatusResponse = (value: unknown): value is UnlockStatusResponse =>
  typeof value === "object" &&
  value !== null &&
  typeof (value as UnlockStatusResponse).requiresBootstrapToken === "boolean";

const isUnlockResponse = (value: unknown): value is UnlockResponse =>
  typeof value === "object" &&
  value !== null &&
  typeof (value as UnlockResponse).ok === "boolean" &&
  typeof (value as UnlockResponse).message === "string";

const readUnlockResponse = async (response: Response): Promise<UnlockResponse> => {
  if (!acceptsJson(response)) {
    return { ok: false, message: response.statusText || "Unlock failed." };
  }

  const body: unknown = await response.json();
  if (isUnlockResponse(body)) {
    return body;
  }

  return { ok: false, message: response.statusText || "Unlock failed." };
};

export const unlockApi = {
  getStatus: async (): Promise<UnlockStatusResponse | null> => {
    const response = await fetch("/api/v1/unlock/status", {
      cache: "no-store",
      headers: { Accept: "application/json" },
    });

    if (!response.ok || !acceptsJson(response)) {
      return null;
    }

    const body: unknown = await response.json();
    return isUnlockStatusResponse(body) ? body : null;
  },

  generateKey: async (): Promise<string> => {
    const response = await fetch("/api/v1/unlock/key", {
      cache: "no-store",
      headers: { Accept: "text/plain" },
    });

    if (!response.ok) {
      throw new Error(response.statusText || "Failed to generate key.");
    }

    return (await response.text()).trim();
  },

  unlock: async (request: {
    masterKey: string;
    bootstrapToken?: string | null;
  }): Promise<UnlockResponse> => {
    const response = await fetch("/api/v1/unlock", {
      method: "POST",
      cache: "no-store",
      headers: jsonHeaders,
      body: JSON.stringify({
        masterKey: request.masterKey,
        bootstrapToken: request.bootstrapToken ?? "",
      }),
    });

    const body = await readUnlockResponse(response);
    if (!response.ok || !body.ok) {
      throw new Error(body.message || response.statusText || "Unlock failed.");
    }

    return body;
  },
};
