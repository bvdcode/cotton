import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { supportedLanguages } from "../../../locales";
import { WizardHeader } from "./WizardHeader";

const mocks = vi.hoisted(() => ({
  setTheme: vi.fn(),
  setUiLanguage: vi.fn(),
}));

vi.mock("../../../app/providers", () => ({
  useTheme: () => ({ mode: "dark", setTheme: mocks.setTheme }),
}));

vi.mock("../../../shared/store/userPreferencesStore", () => ({
  useUserPreferencesStore: <T,>(
    selector: (state: { setUiLanguage: typeof mocks.setUiLanguage }) => T,
  ): T => selector({ setUiLanguage: mocks.setUiLanguage }),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    i18n: { language: "en", resolvedLanguage: "en" },
    t: (key: string) => key,
  }),
}));

describe("WizardHeader", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("selects a language from an anchored menu", () => {
    render(<WizardHeader />);

    fireEvent.click(screen.getByRole("button", { name: "language" }));

    expect(screen.getAllByRole("menuitem")).toHaveLength(
      supportedLanguages.length,
    );
    expect(screen.getByRole("menuitem", { name: "English" })).toHaveAttribute(
      "aria-current",
      "true",
    );

    fireEvent.click(screen.getByRole("menuitem", { name: "Deutsch" }));

    expect(mocks.setUiLanguage).toHaveBeenCalledWith("de");
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });
});
