import { describe, expect, it } from "vitest";
import { darkTheme } from "@shared/theme/darkTheme";
import { lightTheme } from "@shared/theme/lightTheme";
import { buildModelPaletteColors, resolveDefaultModelColor } from "./modelPalette";

describe("resolveDefaultModelColor", () => {
  it("uses the app primary accent as the default model color", () => {
    expect(resolveDefaultModelColor(lightTheme)).toBe(lightTheme.palette.primary.main);
    expect(resolveDefaultModelColor(darkTheme)).toBe(darkTheme.palette.primary.main);
  });

  it("keeps the default model color available in the palette", () => {
    const defaultColor = resolveDefaultModelColor(lightTheme);
    const palette = buildModelPaletteColors(lightTheme);

    expect(palette.some((option) => option.color === defaultColor)).toBe(true);
  });
});
