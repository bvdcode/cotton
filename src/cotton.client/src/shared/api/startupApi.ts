import { httpClient } from "./httpClient";

export interface StartupBlocker {
  kind: string;
  title: string;
  message: string;
  currentVersion?: string | null;
  requiredVersion?: string | null;
  requiredVersionRange?: string | null;
  lastRecordedVersion?: string | null;
}

export interface StartupStatusResponse {
  blocked: boolean;
  blocker?: StartupBlocker | null;
}

let statusPromise: Promise<StartupStatusResponse> | null = null;

const requestStatus = async (): Promise<StartupStatusResponse> => {
  const response = await httpClient.get<StartupStatusResponse>(
    "startup/status",
    {
      headers: {
        "Cache-Control": "no-store",
      },
    },
  );
  return response.data;
};

export const startupApi = {
  getStatus: (): Promise<StartupStatusResponse> => {
    if (statusPromise) {
      return statusPromise;
    }

    const promise = requestStatus();
    statusPromise = promise;
    const clearPromise = (): void => {
      if (statusPromise === promise) {
        statusPromise = null;
      }
    };
    void promise.then(clearPromise, clearPromise);
    return promise;
  },
};
