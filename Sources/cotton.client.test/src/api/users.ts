import api from "./http.ts";
import { API_ENDPOINTS } from "../config.ts";
import type { AuthUser } from "../stores/authStore.ts";

export const getMe = async (): Promise<AuthUser> => {
  const res = await api.get<AuthUser>(`${API_ENDPOINTS.users}/me`);
  return res.data;
};

export default {
  getMe,
};
