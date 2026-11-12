import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { AxiosError } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { filesApi } from "@shared/api/filesApi";
import { layoutsApi } from "@shared/api/layoutsApi";
import { useUserPreferencesStore } from "@shared/store/userPreferencesStore";
import { FileSystemItemCard } from "@pages/files/components/FileSystemItemCard";
import { createHttpResponse } from "../../../test/httpFixtures";
import { ShareLinkDialog } from "@shared/ui/ShareLinkDialog";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("@shared/api/filesApi", () => ({
  filesApi: { getDownloadLink: vi.fn() },
}));
vi.mock("@shared/api/layoutsApi", () => ({
  layoutsApi: { getNodeShareLink: vi.fn() },
}));

const target = { kind: "file", id: "file-1", name: "Photo.jpg" } as const;

afterEach(() => vi.unstubAllGlobals());

const selectUnit = (unit: string) => {
  fireEvent.mouseDown(
    screen.getByRole("combobox", { name: "share.options.unit" }),
  );
  fireEvent.click(
    screen.getByRole("option", { name: `share.options.units.${unit}` }),
  );
};

beforeEach(() => {
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe(): void {}
      disconnect(): void {}
    },
  );
  vi.clearAllMocks();
  useUserPreferencesStore
    .getState()
    .hydrateFromRemote({ shareLinkExpireAfterMinutes: "1440" });
  vi.mocked(filesApi.getDownloadLink).mockResolvedValue("/s/photo-link");
  vi.mocked(layoutsApi.getNodeShareLink).mockResolvedValue("/s/folder-link");
});

describe("ShareLinkDialog", () => {
  it.each([
    [7, "weeks", 1],
    [30, "months", 1],
    [90, "months", 3],
    [365, "years", 1],
    [1095, "years", 3],
    [3650, "years", 10],
  ])(
    "keeps the profile duration of %s days in compact units",
    async (days, unit, value) => {
      useUserPreferencesStore.getState().hydrateFromRemote({
        shareLinkExpireAfterMinutes: String(days * 24 * 60),
      });
      render(<ShareLinkDialog target={target} onClose={vi.fn()} />);
      expect(screen.getByRole("dialog")).toHaveAccessibleName(
        `share.options.title · ${target.name}`,
      );
      expect(screen.getByRole("spinbutton")).toHaveValue(value);
      expect(screen.getByRole("combobox")).toHaveTextContent(
        `share.options.units.${unit}`,
      );
      fireEvent.click(
        screen.getByRole("button", { name: "share.options.create" }),
      );
      await screen.findByRole("textbox", { name: "share.options.link" });
      expect(filesApi.getDownloadLink).toHaveBeenCalledWith(
        target.id,
        days * 24 * 60,
        "",
      );
    },
  );

  it("allows hidden content only with uploads and sends both permissions", async () => {
    render(
      <ShareLinkDialog
        target={{ kind: "folder", id: "folder", name: "Uploads" }}
        onClose={vi.fn()}
      />,
    );
    const uploads = screen.getByRole("switch", {
      name: "share.options.allowUpload",
    });
    const hidden = screen.getByRole("switch", {
      name: "share.options.hideFolderContent",
    });
    expect(hidden).toBeDisabled();
    fireEvent.click(uploads);
    fireEvent.click(hidden);
    expect(hidden).toBeChecked();
    fireEvent.click(uploads);
    expect(hidden).not.toBeChecked();
    expect(hidden).toBeDisabled();
    fireEvent.click(uploads);
    fireEvent.click(hidden);
    fireEvent.click(
      screen.getByRole("button", { name: "share.options.create" }),
    );
    await screen.findByRole("textbox", { name: "share.options.link" });
    expect(layoutsApi.getNodeShareLink).toHaveBeenCalledWith(
      "folder",
      1440,
      "",
      true,
      true,
    );
  });

  it("only offers uploads for folder links and sends the selected permission", async () => {
    render(
      <ShareLinkDialog
        target={{ kind: "folder", id: "folder", name: "Uploads" }}
        onClose={vi.fn()}
      />,
    );
    const permission = screen.getByRole("switch", {
      name: "share.options.allowUpload",
    });
    expect(permission).not.toBeChecked();
    fireEvent.click(permission);
    fireEvent.click(
      screen.getByRole("button", { name: "share.options.create" }),
    );
    await screen.findByRole("textbox", { name: "share.options.link" });
    expect(layoutsApi.getNodeShareLink).toHaveBeenCalledWith(
      "folder",
      1440,
      "",
      true,
      false,
    );
    expect(permission).toBeDisabled();
  });

  it("keeps standard Escape closing available", async () => {
    const close = vi.fn();
    render(<ShareLinkDialog target={target} onClose={close} />);
    fireEvent.keyDown(
      screen.getByRole("textbox", { name: "share.options.name" }),
      {
        key: "Escape",
        code: "Escape",
      },
    );
    await waitFor(() => expect(close).toHaveBeenCalledTimes(1));
  });
  it("creates a named file link with custom duration without changing profile defaults", async () => {
    render(<ShareLinkDialog target={target} onClose={vi.fn()} />);
    expect(
      screen.queryByRole("switch", { name: "share.options.allowUpload" }),
    ).not.toBeInTheDocument();
    expect(filesApi.getDownloadLink).not.toHaveBeenCalled();
    expect(screen.getByRole("spinbutton")).toHaveValue(1);
    fireEvent.change(
      screen.getByRole("textbox", { name: "share.options.name" }),
      { target: { value: "  holiday-Фото  " } },
    );
    fireEvent.change(screen.getByRole("spinbutton"), {
      target: { value: "2" },
    });
    selectUnit("hours");
    fireEvent.click(
      screen.getByRole("button", { name: "share.options.create" }),
    );
    await screen.findByRole("textbox", { name: "share.options.link" });
    expect(filesApi.getDownloadLink).toHaveBeenCalledExactlyOnceWith(
      "file-1",
      120,
      "holiday-Фото",
    );
    expect(
      useUserPreferencesStore.getState().preferences
        .shareLinkExpireAfterMinutes,
    ).toBe("1440");
    expect(layoutsApi.getNodeShareLink).not.toHaveBeenCalled();
  });

  it("creates unlimited folder links and copies the public URL without regenerating it", async () => {
    const copy = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText: copy },
    });
    render(
      <ShareLinkDialog
        target={{ kind: "folder", id: "folder-1", name: "Books" }}
        onClose={vi.fn()}
      />,
    );
    selectUnit("never");
    expect(screen.queryByRole("spinbutton")).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole("button", { name: "share.options.create" }),
    );
    const link = await screen.findByRole("textbox", {
      name: "share.options.link",
    });
    await waitFor(() => expect(link).toHaveFocus());
    expect(link).toHaveValue(`${window.location.origin}/s/folder-link`);
    expect(layoutsApi.getNodeShareLink).toHaveBeenCalledExactlyOnceWith(
      "folder-1",
      null,
      "",
      false,
      false,
    );
    fireEvent.click(
      screen.getByRole("button", { name: "common:actions.copy" }),
    );
    await waitFor(() =>
      expect(copy).toHaveBeenCalledWith(
        `${window.location.origin}/s/folder-link`,
      ),
    );
    expect(layoutsApi.getNodeShareLink).toHaveBeenCalledTimes(1);
    expect(
      screen.queryByRole("button", { name: "share.options.create" }),
    ).not.toBeInTheDocument();
  });

  it("keeps the form editable when the custom address is already taken", async () => {
    vi.mocked(filesApi.getDownloadLink).mockRejectedValueOnce(
      new AxiosError("Conflict", "409", undefined, undefined, {
        ...createHttpResponse({}),
        status: 409,
      }),
    );
    render(<ShareLinkDialog target={target} onClose={vi.fn()} />);
    fireEvent.change(
      screen.getByRole("textbox", { name: "share.options.name" }),
      { target: { value: "photo" } },
    );
    fireEvent.click(
      screen.getByRole("button", { name: "share.options.create" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "share.options.nameTaken",
    );
    fireEvent.change(
      screen.getByRole("textbox", { name: "share.options.name" }),
      { target: { value: "photo-2" } },
    );
    fireEvent.click(
      screen.getByRole("button", { name: "share.options.create" }),
    );
    await screen.findByRole("textbox", { name: "share.options.link" });
    expect(filesApi.getDownloadLink).toHaveBeenLastCalledWith(
      "file-1",
      1440,
      "photo-2",
    );
  });

  it("rejects URL delimiters and durations outside server limits", () => {
    render(<ShareLinkDialog target={target} onClose={vi.fn()} />);
    const create = screen.getByRole("button", { name: "share.options.create" });
    fireEvent.change(
      screen.getByRole("textbox", { name: "share.options.name" }),
      { target: { value: "books?x=1" } },
    );
    expect(create).toBeDisabled();
    fireEvent.change(
      screen.getByRole("textbox", { name: "share.options.name" }),
      { target: { value: "" } },
    );
    for (const value of ["0", "-1", "3651", ""]) {
      fireEvent.change(screen.getByRole("spinbutton"), { target: { value } });
      expect(create).toBeDisabled();
    }
    fireEvent.change(screen.getByRole("spinbutton"), {
      target: { value: "3650" },
    });
    expect(create).toBeEnabled();
    expect(filesApi.getDownloadLink).not.toHaveBeenCalled();
  });

  it("preserves unlimited profile defaults", () => {
    useUserPreferencesStore
      .getState()
      .hydrateFromRemote({ shareLinkExpireAfterMinutes: "null" });
    render(<ShareLinkDialog target={target} onClose={vi.fn()} />);
    expect(screen.getByRole("combobox")).toHaveTextContent(
      "share.options.units.never",
    );
    expect(screen.queryByRole("spinbutton")).not.toBeInTheDocument();
  });

  it("opens options separately from quick sharing and does not navigate the card", async () => {
    const quickShare = vi.fn();
    const navigate = vi.fn();
    render(
      <FileSystemItemCard
        title="Photo.jpg"
        icon={null}
        onClick={navigate}
        actions={[
          {
            icon: null,
            tooltip: "Share",
            onClick: quickShare,
            shareTarget: target,
          },
        ]}
      />,
    );
    const menuButton =
      document.querySelector<HTMLButtonElement>(".card-menu-button");
    if (!menuButton) {
      throw new Error("Menu button missing");
    }
    fireEvent.click(menuButton);
    fireEvent.click(
      screen.getByRole("button", { name: "share.options.title" }),
    );
    const dialog = await screen.findByRole("dialog");
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
    expect(quickShare).not.toHaveBeenCalled();
    fireEvent.click(
      screen.getByRole("textbox", { name: "share.options.name" }),
    );
    fireEvent.keyDown(dialog, { key: "Enter" });
    expect(navigate).not.toHaveBeenCalled();
    fireEvent.click(
      screen.getByRole("button", { name: "common:actions.cancel" }),
    );
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    fireEvent.click(menuButton);
    fireEvent.click(screen.getByRole("menuitem", { name: /Share/ }));
    expect(quickShare).toHaveBeenCalledTimes(1);
    expect(navigate).not.toHaveBeenCalled();
  });
});
