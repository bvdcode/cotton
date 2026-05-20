import { httpClient } from "./httpClient";

export interface UserStorageQuotaDto {
  usedBytes: number;
  quotaBytes: number | null;
  availableBytes: number | null;
}

export const storageQuotaApi = {
  getCurrent: async (): Promise<UserStorageQuotaDto> => {
    const response = await httpClient.get<UserStorageQuotaDto>(
      "users/me/storage-quota",
    );
    return response.data;
  },
};
