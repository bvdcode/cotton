import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { UserMenuAppDownloads } from "./UserMenuAppDownloads";

const downloadLinks = [
  {
    label: "Google Play testing",
    url: "https://play.google.com/apps/testing/dev.cottoncloud.app",
  },
  {
    label: "Android APK",
    url: "https://github.com/bvdcode/cotton-mobile/releases/latest/download/CottonCloud-Android.apk",
  },
  {
    label: "Windows installer",
    url: "https://github.com/bvdcode/cotton-sync-client/releases/latest/download/CottonSync-Windows-Setup.exe",
  },
  {
    label: "Linux DEB package",
    url: "https://github.com/bvdcode/cotton-sync-client/releases/latest/download/CottonSync-Linux.deb",
  },
] as const;

describe("UserMenuAppDownloads", () => {
  it("renders external download links in platform order", () => {
    render(<UserMenuAppDownloads onOpenLink={vi.fn()} />);

    for (const link of downloadLinks) {
      const element = screen.getByRole("link", { name: link.label });

      expect(element).toHaveAttribute("href", link.url);
      expect(element).toHaveAttribute("target", "_blank");
      expect(element.getAttribute("rel")).toContain("noopener");
      expect(element.getAttribute("rel")).toContain("noreferrer");
    }

    expect(
      screen
        .getAllByRole("link")
        .map((element) => element.getAttribute("aria-label")),
    ).toEqual(downloadLinks.map((link) => link.label));
  });

  it("notifies the parent before opening a download link", () => {
    const onOpenLink = vi.fn();

    render(<UserMenuAppDownloads onOpenLink={onOpenLink} />);

    fireEvent.click(screen.getByRole("link", { name: "Android APK" }));

    expect(onOpenLink).toHaveBeenCalledTimes(1);
  });
});
