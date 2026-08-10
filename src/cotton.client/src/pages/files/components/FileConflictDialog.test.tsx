import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ConflictAction } from "../utils/uploadConflicts";
import { FileConflictDialog } from "./FileConflictDialog";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string, options?: { newName?: string }) => {
      if (key === "conflicts.rename") {
        return `Upload as ${options?.newName}`;
      }

      return key;
    },
  }),
}));

function renderDialog(
  onResolve: (action: ConflictAction) => void,
  canOverwrite = true,
): void {
  render(
    <FileConflictDialog
      open
      newName="a-very-long-generated-file-name.jpg"
      canOverwrite={canOverwrite}
      onResolve={onResolve}
      onExited={vi.fn()}
    />,
  );
}

describe("FileConflictDialog", () => {
  it("preserves every conflict resolution action", () => {
    const onResolve = vi.fn();
    renderDialog(onResolve);

    fireEvent.click(
      screen.getByRole("button", { name: "common:actions.cancel" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "conflicts.skip" }));
    fireEvent.click(screen.getByRole("button", { name: "conflicts.skipAll" }));
    fireEvent.click(
      screen.getByRole("button", {
        name: "Upload as a-very-long-generated-file-name.jpg",
      }),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "conflicts.overwrite" }),
    );

    expect(onResolve.mock.calls).toEqual([
      [ConflictAction.Cancel],
      [ConflictAction.Skip],
      [ConflictAction.SkipAll],
      [ConflictAction.Rename],
      [ConflictAction.Overwrite],
    ]);
  });

  it("hides overwrite when replacing is unavailable", () => {
    renderDialog(vi.fn(), false);

    expect(
      screen.queryByRole("button", { name: "conflicts.overwrite" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", {
        name: "Upload as a-very-long-generated-file-name.jpg",
      }),
    ).toBeInTheDocument();
  });
});
