import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { TotpSetupForm } from "./TotpSetupForm";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe("TotpSetupForm", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders the QR code after setup data is generated", () => {
    const { container } = render(
      <TotpSetupForm
        totpSetup={{
          secretBase32: "JBSWY3DPEHPK3PXP",
          otpAuthUri:
            "otpauth://totp/cotton:alice?secret=JBSWY3DPEHPK3PXP&issuer=cotton",
        }}
        totpCode=""
        totpConfirmLoading={false}
        onTotpCodeChange={vi.fn()}
        onConfirm={vi.fn()}
        onCopySecret={vi.fn()}
      />,
    );

    expect(screen.getByText("JBSWY3DPEHPK3PXP")).toBeInTheDocument();
    expect(container.querySelector("svg")).not.toBeNull();
  });
});
