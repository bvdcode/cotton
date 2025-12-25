import { useSettingsStore } from "./settingsStore";

export function useServerSettings() {
  const data = useSettingsStore((s) => s.data);
  const loading = useSettingsStore((s) => s.loading);
  const loaded = useSettingsStore((s) => s.loaded);
  const error = useSettingsStore((s) => s.error);
  const lastUpdated = useSettingsStore((s) => s.lastUpdated);
  const fetchSettings = useSettingsStore((s) => s.fetchSettings);

  return { data, loading, loaded, error, lastUpdated, fetchSettings };
}
