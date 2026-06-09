import { act, cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  useSearchPagination,
  type SearchPaginationState,
} from "./useSearchPagination";

const mocks = vi.hoisted(() => ({
  search: vi.fn(),
}));

vi.mock("../../../shared/api/layoutsApi", () => ({
  layoutsApi: {
    search: mocks.search,
  },
}));

interface ObservedSearchState {
  state: SearchPaginationState;
  searchCallsAtRender: number;
}

const observedStates: ObservedSearchState[] = [];

const SearchPaginationProbe = ({
  trimmedQuery,
}: {
  trimmedQuery: string;
}) => {
  const state = useSearchPagination({
    trimmedQuery,
    layoutId: "layout-1",
  });

  observedStates.push({
    state,
    searchCallsAtRender: mocks.search.mock.calls.length,
  });

  return null;
};

describe("useSearchPagination", () => {
  afterEach(() => {
    cleanup();
    observedStates.length = 0;
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it("reports initial loading during the render that activates a debounced search key", () => {
    vi.useFakeTimers();
    mocks.search.mockReturnValue(new Promise(() => {}));

    const { rerender } = render(<SearchPaginationProbe trimmedQuery="" />);

    rerender(<SearchPaginationProbe trimmedQuery="report" />);

    act(() => {
      vi.advanceTimersByTime(300);
    });

    const firstDebouncedRender = observedStates.find(
      ({ searchCallsAtRender, state }) =>
        state.debouncedQuery === "report" && searchCallsAtRender === 0,
    );

    expect(firstDebouncedRender?.state.loadingInitial).toBe(true);
    expect(firstDebouncedRender?.state.results).toBeNull();
    expect(firstDebouncedRender?.state.totalCount).toBe(0);
  });
});
