import {
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";
import {
  decodeEnvelopePreference,
  encodeEnvelopePreference,
  ENVELOPE_PREFERENCE_KEY,
} from "./envelope";

export function hasEnvelopePreference(
  preferences: UserPreferences | null | undefined,
): boolean {
  const value = preferences?.[ENVELOPE_PREFERENCE_KEY];
  return typeof value === "string" && value.length > 0;
}

export function readEnvelopeFromPreferences(
  preferences: UserPreferences | null | undefined,
): Uint8Array | null {
  const value = preferences?.[ENVELOPE_PREFERENCE_KEY];

  if (typeof value !== "string" || value.length === 0) {
    return null;
  }

  try {
    return decodeEnvelopePreference(value);
  } catch {
    return null;
  }
}

export async function persistEnvelope(
  envelope: Uint8Array,
): Promise<UserPreferences> {
  return userPreferencesApi.update({
    [ENVELOPE_PREFERENCE_KEY]: encodeEnvelopePreference(envelope),
  });
}
