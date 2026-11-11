import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useLongPress } from "@shared/hooks/useLongPress";
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

function SharingHeader({
  onClick,
  onHold,
}: {
  onClick: () => void;
  onHold: () => void;
}) {
  const hold = useLongPress(onHold);
  return (
    <PageHeader
      {...defaultProps}
      customActionItems={[
        {
          key: "share",
          title: "Share",
          icon: <span />,
          onClick,
          hold: { ...hold, icon: <span />, title: "Share settings" },
        },
      ]}
    />
  );
}

it("keeps primary folder actions before creation actions and exposes their toggle state", () => {
  const toggle = vi.fn();
  renderHeader({
    primaryActionItems: [
      {
        key: "favorite",
        title: "Favorite",
        icon: <span />,
        onClick: toggle,
        active: true,
      },
    ],
    showUpload: true,
    onUploadClick: vi.fn(),
  });
  const favorite = screen.getByRole("button", { name: "Favorite" });
  expect(favorite).toHaveAttribute("aria-pressed", "true");
  const buttons = screen.getAllByRole("button");
  expect(buttons.indexOf(favorite)).toBeLessThan(
    buttons.indexOf(screen.getByRole("button", { name: "actions.upload" })),
  );
  fireEvent.click(favorite);
  expect(toggle).toHaveBeenCalledOnce();
});

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
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("PageHeader", () => {
  it("preserves the hold action and short click when sharing moves into overflow", () => {
    vi.useFakeTimers();
    vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(64);
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockReturnValue(
      DOMRect.fromRect({ width: 40, height: 40 }),
    );
    const onClick = vi.fn();
    const onHold = vi.fn();
    render(<SharingHeader onClick={onClick} onHold={onHold} />);
    fireEvent.click(
      screen.getByRole("button", { name: "common:actions.more" }),
    );
    const item = screen.getByRole("menuitem", { name: "Share" });
    fireEvent(
      item,
      Object.assign(new Event("pointerdown", { bubbles: true }), {
        pointerId: 1,
        isPrimary: true,
        button: 0,
      }),
    );
    act(() => vi.advanceTimersByTime(600));
    expect(item).toHaveTextContent("Share settings");
    expect(onHold).not.toHaveBeenCalled();
    fireEvent(
      item,
      Object.assign(new Event("pointerup", { bubbles: true }), {
        pointerId: 1,
      }),
    );
    fireEvent.click(item, { detail: 1 });
    expect(onHold).toHaveBeenCalledOnce();
    expect(onClick).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(300));
    expect(screen.queryByRole("menu")).toBeNull();
    fireEvent.click(
      screen.getByRole("button", { name: "common:actions.more" }),
    );
    fireEvent.click(screen.getByRole("menuitem", { name: "Share" }));
    expect(onClick).toHaveBeenCalledOnce();
    expect(onHold).toHaveBeenCalledOnce();
  });

  it("uses overflow only when the minimum button widths exceed available space", () => {
    vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(320);
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(
      function (this: HTMLElement) {
        const hidden =
          this.parentElement &&
          window.getComputedStyle(this.parentElement).display === "none";
        return DOMRect.fromRect({ width: hidden ? 0 : 40, height: 40 });
      },
    );
    const actions = Array.from({ length: 6 }, (_, index) => ({
      key: `action-${index}`,
      icon: <span />,
      title: `Action ${index}`,
      onClick: vi.fn(),
    }));
    const view = render(
      <PageHeader {...defaultProps} primaryActionItems={actions} />,
    );
    expect(
      screen.queryByRole("button", { name: "common:actions.more" }),
    ).toBeNull();
    expect(screen.getAllByRole("button")).toHaveLength(8);
    view.rerender(
      <PageHeader
        {...defaultProps}
        primaryActionItems={[
          ...actions,
          {
            key: "extra",
            icon: <span />,
            title: "Extra",
            onClick: vi.fn(),
          },
        ]}
      />,
    );
    expect(
      screen.getByRole("button", { name: "common:actions.more" }),
    ).toBeVisible();
    expect(screen.getAllByRole("button")).toHaveLength(8);
  });

  it("keeps navigation on go-up without a separate home button", () => {
    const onGoUp = vi.fn();
    renderHeader({ canGoUp: true, onGoUp });
    expect(
      screen.queryByRole("button", { name: "breadcrumbs.root" }),
    ).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "actions.goUp" }));
    expect(onGoUp).toHaveBeenCalledOnce();
  });

  it("keeps overflow actions stable when hidden buttons cannot be measured", () => {
    vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(64);
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(
      function (this: HTMLElement) {
        const parentDisplay = this.parentElement
          ? window.getComputedStyle(this.parentElement).display
          : "";
        const width = parentDisplay === "none" ? 0 : 40;

        return DOMRect.fromRect({ width, height: 40 });
      },
    );

    renderHeader();

    expect(
      screen.getByRole("button", { name: "common:actions.more" }),
    ).toBeVisible();
  });

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

it("marks only the changing size as busy and clears that state on completion", () => {
  const { rerender } = render(
    <PageHeader
      {...defaultProps}
      stats={{ folders: 2, files: 3, sizeBytes: 1024 }}
      isSizeCalculating
    />,
  );
  expect(screen.getByText("1.00 KB")).toHaveAttribute("aria-busy", "true");
  rerender(
    <PageHeader
      {...defaultProps}
      stats={{ folders: 2, files: 3, sizeBytes: 2048 }}
    />,
  );
  expect(screen.getByText("2.00 KB")).toHaveAttribute("aria-busy", "false");
});
