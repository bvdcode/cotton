import createCache from "@emotion/cache";
import { CacheProvider } from "@emotion/react";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, it } from "vitest";
import { WidgetHeader } from "./WidgetHeader";

afterEach(cleanup);

it("updates progress without accumulating styles for every transferred byte count", () => {
  const cache = createCache({ key: "task-progress" });
  const header = (progressPercent: number) => (
    <CacheProvider value={cache}>
      <WidgetHeader
        title="Tasks"
        isCollapsed={false}
        hasActive
        progressPercent={progressPercent}
        onToggleCollapse={() => {}}
        onClose={() => {}}
        aria={{ expand: "Expand", collapse: "Collapse", close: "Close" }}
      />
    </CacheProvider>
  );
  const { rerender, unmount } = render(header(0));
  const initialStyles = Object.keys(cache.inserted).length;
  expect(initialStyles).toBeGreaterThan(0);

  for (let update = 1; update <= 100; update += 1) {
    rerender(header(update * 0.123456789));
  }

  expect(Object.keys(cache.inserted)).toHaveLength(initialStyles);
  expect(screen.getByRole("progressbar", { name: "Tasks" })).toHaveAttribute(
    "aria-valuenow",
    "12.3456789",
  );
  unmount();
  cache.sheet.flush();
});
