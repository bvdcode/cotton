import type { AxiosInstance } from "axios";
import { getHttpOrThrow } from "./http";
import type { AuthUser } from "../types/api";

const BASE = "/api/v1/users";

class UsersApiClient {
  private axios(): AxiosInstance {
    return getHttpOrThrow();
  }

  async getMe(): Promise<AuthUser> {
    const { data } = await this.axios().get<AuthUser>(`${BASE}/me`);
    return data;
  }
}

export const usersApi = new UsersApiClient();
export type { UsersApiClient };
