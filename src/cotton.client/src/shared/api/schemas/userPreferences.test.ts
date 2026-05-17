import { describe, expect, it } from "vitest";
import { userPreferencesSchema } from "./userPreferences";

describe("userPreferencesSchema", () => {
  it("accepts string preference dictionaries", () => {
    expect(
      userPreferencesSchema.parse({
        themeMode: "dark",
        uiLanguage: "en",
      }),
    ).toEqual({
      themeMode: "dark",
      uiLanguage: "en",
    });
  });

  it("rejects non-string preference values", () => {
    expect(() =>
      userPreferencesSchema.parse({ notificationSoundEnabled: true }),
    ).toThrow();
  });
});
