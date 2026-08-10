import PolicyIcon from "@mui/icons-material/Policy";
import SecurityIcon from "@mui/icons-material/Security";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import {
  DesktopAdminNavigation,
  MobileAdminNavigation,
} from "./AdminNavigation";
import type { AdminMenuSection } from "./adminNavigationModel";

const sections: AdminMenuSection[] = [
  {
    id: "security",
    title: "Security",
    items: [
      {
        id: "privacySettings",
        to: "/admin/privacy-settings",
        title: "Privacy",
        icon: PolicyIcon,
      },
      {
        id: "securityDiagnostics",
        to: "/admin/security",
        title: "Security checkup",
        icon: SecurityIcon,
      },
    ],
  },
];

describe("AdminNavigation", () => {
  it("renders accessible links and marks the current page", () => {
    const onToggle = vi.fn();

    render(
      <MemoryRouter initialEntries={["/admin/privacy-settings"]}>
        <DesktopAdminNavigation
          sections={sections}
          expanded
          onToggle={onToggle}
          navigationLabel="Administration"
          toggleLabel="Collapse navigation"
        />
      </MemoryRouter>,
    );

    const navigation = screen.getByRole("navigation", {
      name: "Administration",
    });
    const privacyLink = within(navigation).getByRole("link", {
      name: "Privacy",
    });

    expect(privacyLink).toHaveAttribute("href", "/admin/privacy-settings");
    expect(privacyLink).toHaveAttribute("aria-current", "page");
    expect(within(navigation).getByText("Security")).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole("button", { name: "Collapse navigation" }),
    );
    expect(onToggle).toHaveBeenCalledOnce();
  });

  it("keeps collapsed navigation discoverable to assistive technology", () => {
    render(
      <MemoryRouter initialEntries={["/admin/security"]}>
        <DesktopAdminNavigation
          sections={sections}
          expanded={false}
          onToggle={() => undefined}
          navigationLabel="Administration"
          toggleLabel="Expand navigation"
        />
      </MemoryRouter>,
    );

    const navigation = screen.getByRole("navigation", {
      name: "Administration",
    });
    expect(
      within(navigation).getByRole("link", { name: "Security checkup" }),
    ).toHaveAttribute("aria-current", "page");
    expect(within(navigation).queryByText("Security")).not.toBeInTheDocument();
  });

  it("labels the mobile section picker", () => {
    render(
      <MobileAdminNavigation
        sections={sections}
        selectedTo="/admin/privacy-settings"
        label="Section"
        onChange={() => undefined}
      />,
    );

    expect(screen.getByRole("combobox", { name: "Section" })).toHaveTextContent(
      "Privacy",
    );
  });
});
