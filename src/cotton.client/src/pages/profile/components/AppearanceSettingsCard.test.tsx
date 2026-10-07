import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { expect, it, vi } from "vitest";
import i18n from "../../../i18n";
import { userPreferencesApi } from "../../../shared/api/userPreferencesApi";
import { useUserPreferencesStore } from "../../../shared/store/userPreferencesStore";
import { AppearanceSettingsCard } from "./AppearanceSettingsCard";

it("defaults to the right edge and saves left, hidden, and right through profile preferences", async () => {
  await i18n.changeLanguage("en");
  useUserPreferencesStore.getState().reset();
  const update = vi
    .spyOn(userPreferencesApi, "update")
    .mockImplementation(async (patch) => patch);
  render(<AppearanceSettingsCard />);
  fireEvent.click(screen.getByRole("button", { name: /Appearance/ }));
  const group = screen.getByRole("group", {
    name: "Show metadata when viewing photos and videos",
  });
  expect(within(group).getByRole("button", { name: "Right" })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  for (const [label, position] of [
    ["Left", "left"],
    ["Hidden", "hidden"],
    ["Right", "right"],
  ]) {
    fireEvent.click(within(group).getByRole("button", { name: label }));
    await waitFor(() =>
      expect(update).toHaveBeenLastCalledWith({
        galleryMetadataPosition: position,
      }),
    );
    expect(within(group).getByRole("button", { name: label })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  }
  update.mockClear();
  fireEvent.click(within(group).getByRole("button", { name: "Right" }));
  expect(update).not.toHaveBeenCalled();
  update.mockRestore();
});
