import { beforeEach, describe, expect, it, vi } from "vitest";
import { ENVELOPE_PREFERENCE_KEY, encodeEnvelopePreference } from "./envelope";
import {
  hasEnvelopePreference,
  persistEnvelope,
  readEnvelopeFromPreferences,
} from "./envelopeStorage";
import { userPreferencesApi } from "../api/userPreferencesApi";

vi.mock("../api/userPreferencesApi", () => ({
  userPreferencesApi: {
    update: vi.fn(),
  },
}));

const mockedUpdate = vi.mocked(userPreferencesApi.update);

describe("envelopeStorage", () => {
  beforeEach(() => {
    mockedUpdate.mockReset();
  });

  it("detects and reads a stored envelope", () => {
    const envelope = new Uint8Array([1, 2, 3, 4]);
    const preferences = {
      [ENVELOPE_PREFERENCE_KEY]: encodeEnvelopePreference(envelope),
    };

    expect(hasEnvelopePreference(preferences)).toBe(true);
    expect(Array.from(readEnvelopeFromPreferences(preferences) ?? [])).toEqual(
      Array.from(envelope),
    );
  });

  it("returns null for missing or malformed values", () => {
    expect(hasEnvelopePreference(undefined)).toBe(false);
    expect(readEnvelopeFromPreferences(undefined)).toBeNull();
    expect(readEnvelopeFromPreferences({ [ENVELOPE_PREFERENCE_KEY]: "not base64!" })).toBeNull();
  });

  it("persists the envelope under the opaque preference key", async () => {
    const envelope = new Uint8Array([5, 6, 7]);
    const expected = encodeEnvelopePreference(envelope);
    mockedUpdate.mockResolvedValue({ [ENVELOPE_PREFERENCE_KEY]: expected });

    await expect(persistEnvelope(envelope)).resolves.toEqual({
      [ENVELOPE_PREFERENCE_KEY]: expected,
    });
    expect(mockedUpdate).toHaveBeenCalledWith({
      [ENVELOPE_PREFERENCE_KEY]: expected,
    });
  });
});
