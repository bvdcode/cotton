import { create } from "zustand";
import { API_ENDPOINTS } from "../config.ts";
import api from "../api/http.ts";

export type SupportedHashAlgorithm = "SHA256" | "SHA-256" | "SHA1" | "MD5";

export interface ServerSettings {
  maxChunkSizeBytes: number;
  supportedHashAlgorithm: SupportedHashAlgorithm;
}

interface SettingsState {
  settings: ServerSettings | null;
  loading: boolean;
  error: string | null;
  load: () => Promise<void>;
}

export const useSettings = create<SettingsState>((set) => ({
  settings: null,
  loading: false,
  error: null,
  load: async () => {
    set({ loading: true, error: null });
    try {
  const res = await api.get<ServerSettings>(`${API_ENDPOINTS.settings}`);
  const data = res.data;
      set({ settings: data, loading: false });
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Unknown error";
      set({ error: msg, loading: false });
    }
  },
}));

export default useSettings;
