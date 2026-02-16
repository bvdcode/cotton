import { useEffect } from "react";
import { eventHub } from "../signalr";
import { isSelfUpdateToken, useUserPreferencesStore } from "./userPreferencesStore";
import { useAuth } from "../../features/auth";
import type { UserPreferences } from "../api/userPreferencesApi";
import { isJsonObject, type JsonValue } from "../types/json";

const HUB_METHOD = "PreferencesUpdated";

const isUserPreferences = (value: JsonValue): value is UserPreferences => {
  if (!isJsonObject(value)) return false;
  if (Array.isArray(value)) return false;

  for (const raw of Object.values(value)) {
    if (typeof raw !== "string") {
      return false;
    }
  }

  return true;
};

const isPreferencesUpdatedArgs = (
  args: JsonValue[],
): args is [string, UserPreferences] => {
  const token = args[0];
  const preferences = args[1];
  return typeof token === "string" && isUserPreferences(preferences);
};

export function useUserPreferencesRealtimeEvents(): void {
  const { isAuthenticated } = useAuth();

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    eventHub.start().catch(() => {
      // connection will retry automatically
    });

    const unsubscribe = eventHub.on(HUB_METHOD, (...args: JsonValue[]) => {
      if (!isPreferencesUpdatedArgs(args)) {
        return;
      }

      const [token, preferences] = args;
      if (isSelfUpdateToken(token)) {
        return;
      }

      useUserPreferencesStore.getState().hydrateFromRemote(preferences);
    });

    return () => {
      unsubscribe();
    };
  }, [isAuthenticated]);
}
