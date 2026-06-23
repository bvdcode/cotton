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

export const startupApi = {
  getStatus: async (): Promise<StartupStatusResponse> => {
    const response = await httpClient.get<StartupStatusResponse>(
      "startup/status",
      {
        headers: {
          "Cache-Control": "no-store",
        },
      },
    );
    return response.data;
  },
};
