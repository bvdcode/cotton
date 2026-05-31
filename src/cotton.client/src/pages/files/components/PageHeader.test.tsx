import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PageHeader, type PageHeaderProps } from "./PageHeader";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

class TestResizeObserver {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

const defaultProps: PageHeaderProps = {
  loading: false,
  breadcrumbs: [],
  stats: { folders: 0, files: 0, sizeBytes: 0 },
  viewMode: "tiles-medium",
  canGoUp: false,
  onGoUp: vi.fn(),
  onHomeClick: vi.fn(),
  onViewModeCycle: vi.fn(),
};

function renderHeader(overrides: Partial<PageHeaderProps> = {}): void {
  render(<PageHeader {...defaultProps} {...overrides} />);
}

beforeEach(() => {
  Object.defineProperty(window, "ResizeObserver", {
    configurable: true,
    writable: true,
    value: TestResizeObserver,
  });
  Object.defineProperty(globalThis, "ResizeObserver", {
    configurable: true,
    writable: true,
    value: TestResizeObserver,
  });
  vi.spyOn(window, "requestAnimationFrame").mockImplementation((callback) => {
    callback(0);
    return 0;
  });
  vi.spyOn(window, "cancelAnimationFrame").mockImplementation(() => undefined);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("PageHeader", () => {
  it("renders and invokes the new markdown file action", () => {
    const onNewFileClick = vi.fn();

    renderHeader({
      showNewFile: true,
      onNewFileClick,
    });

    fireEvent.click(
      screen.getByRole("button", { name: "actions.newMarkdownFile" }),
    );

    expect(onNewFileClick).toHaveBeenCalledOnce();
  });

  it("disables the new markdown file action while creating a file", () => {
    renderHeader({
      showNewFile: true,
      onNewFileClick: vi.fn(),
      isCreatingFile: true,
    });

    expect(
      screen.getByRole("button", { name: "actions.newMarkdownFile" }),
    ).toBeDisabled();
  });
});
