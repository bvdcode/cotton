import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { AppTask } from "../../../shared/tasks";
import { UploadTaskRow } from "./UploadTaskRow";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

describe("UploadTaskRow", () => {
  it("shows the exact failure reason in a tooltip", async () => {
    const errorDetails = "The server rejected the uploaded chunk.";
    const task: AppTask = {
      id: "upload-1",
      kind: "upload",
      label: "photo.jpg",
      scopeLabel: "Pictures",
      bytesTotal: 1024,
      bytesCompleted: 1024,
      progress01: 1,
      status: "failed",
      error: errorDetails,
      errorKey: "uploadFailed",
    };

    render(<UploadTaskRow task={task} showDivider={false} />);

    expect(screen.getByText("errors.uploadFailed")).toBeInTheDocument();
    const errorIcon = screen.getByLabelText(errorDetails);

    fireEvent.mouseOver(errorIcon);

    expect(await screen.findByRole("tooltip")).toHaveTextContent(errorDetails);
  });
});
