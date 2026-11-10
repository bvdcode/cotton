import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Dialog, ThemeProvider } from "@mui/material";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RoutineScheduleKind } from "@shared/api/routinesApi";
import { darkTheme } from "@shared/theme";
import { createFolder } from "../../test/fileFixtures";
import { httpClient } from "@shared/api/httpClient";
import { createHttpResponse } from "../../test/httpFixtures";
import { defaultRoutine } from "./routineForm";
import { RoutineDialog } from "./RoutineDialog";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: "en" } }),
}));
vi.mock("@shared/ui/FolderPickerDialog", () => ({
  FolderPickerDialog: ({
    onSelect,
    onClose,
  }: {
    onSelect: (folder: ReturnType<typeof createFolder>) => void;
    onClose: () => void;
  }) => (
    <Dialog open>
      <button
        onClick={() => {
          onSelect(createFolder());
          onClose();
        }}
      >
        pick
      </button>
    </Dialog>
  ),
}));
afterEach(() => vi.restoreAllMocks());
const show = () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <ThemeProvider theme={darkTheme}>
      <QueryClientProvider client={client}>
        <RoutineDialog
          routine={null}
          timezone="America/Los_Angeles"
          onClose={vi.fn()}
          onSaved={vi.fn().mockResolvedValue(undefined)}
        />
      </QueryClientProvider>
    </ThemeProvider>,
  );
};
describe("routine configuration", () => {
  it("keeps advanced settings collapsed and submits the selected folder", async () => {
    const save = vi.spyOn(httpClient, "post").mockResolvedValue(
      createHttpResponse({
        ...defaultRoutine(),
        targetNodeId: "folder-1",
        name: "Cleanup",
        id: "routine",
        createdAt: "2026-11-01T00:00:00Z",
        updatedAt: "2026-11-01T00:00:00Z",
        lastExecutionAt: null,
        nextExecutionAt: null,
        latestRun: null,
        targetNode: null,
        timezone: "UTC",
      }),
    );
    show();
    expect(
      screen.getByRole("button", { name: "moreSettings" }),
    ).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByRole("button", { name: "createAndRun" })).toBeDisabled();
    fireEvent.change(screen.getByRole("textbox", { name: "name" }), {
      target: { value: "  Cleanup  " },
    });
    fireEvent.click(screen.getByRole("button", { name: "chooseFolder" }));
    fireEvent.click(screen.getByRole("button", { name: "pick" }));
    fireEvent.click(screen.getByRole("button", { name: "createAndRun" }));
    await waitFor(() =>
      expect(save).toHaveBeenCalledExactlyOnceWith(
        "/routines",
        expect.objectContaining({
          name: "Cleanup",
          targetNodeId: "folder-1",
          scheduleKind: RoutineScheduleKind.Interval,
        }),
      ),
    );
  });
  it("reveals calendar settings without losing cleanup conditions", async () => {
    show();
    fireEvent.change(
      screen.getByRole("spinbutton", { name: "olderThanDays" }),
      { target: { value: "45" } },
    );
    fireEvent.click(screen.getByRole("button", { name: "moreSettings" }));
    fireEvent.click(
      await screen.findByRole("switch", { name: "calendarMode" }),
    );
    expect(await screen.findByLabelText("time")).toHaveValue("00:00");
    expect(
      screen.getByRole("spinbutton", { name: "olderThanDays" }),
    ).toHaveValue(45);
    expect(screen.getAllByRole("combobox", { name: "frequency" })).toHaveLength(
      1,
    );
  });
});
