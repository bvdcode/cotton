import {
  act,
  cleanup,
  render,
  renderHook,
  waitFor,
} from "@testing-library/react";
import type {
  LayoutSearchResult,
  layoutsApi,
} from "../../../shared/api/layoutsApi";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  useSearchPagination,
  type SearchPaginationState,
} from "./useSearchPagination";

const mocks = vi.hoisted(() => ({
  search: vi.fn<typeof layoutsApi.search>(),
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

const SearchPaginationProbe = ({ trimmedQuery }: { trimmedQuery: string }) => {
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

  it("restarts pagination when the folder changes and ignores the old folder response", async () => {
    const response = (id: string): LayoutSearchResult => ({
      data: {
        orderedIds: [id],
        nodes: [
          {
            id,
            name: id,
            parentId: "folder-1",
            layoutId: "layout-1",
            metadata: {},
            createdAt: "",
            updatedAt: "",
          },
        ],
        files: [],
        nodePaths: {},
        filePaths: {},
      },
      totalCount: 2,
    });
    let finishOldPage: (value: LayoutSearchResult) => void = () => {};
    mocks.search
      .mockResolvedValueOnce(response("old-first"))
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            finishOldPage = resolve;
          }),
      )
      .mockResolvedValueOnce(response("new-first"));

    const { result, rerender } = renderHook(
      ({ currentNodeId }) =>
        useSearchPagination({
          layoutId: "layout-1",
          trimmedQuery: "report",
          currentNodeId,
        }),
      { initialProps: { currentNodeId: "folder-1" } },
    );
    await waitFor(() =>
      expect(result.current.results?.nodes[0].id).toBe("old-first"),
    );
    act(() => result.current.loadNextPage());
    expect(mocks.search).toHaveBeenLastCalledWith(
      expect.objectContaining({
        currentNodeId: "folder-1",
        page: 2,
      }),
    );
    const oldSignal = mocks.search.mock.lastCall?.[0].signal;

    rerender({ currentNodeId: "folder-2" });
    await waitFor(() =>
      expect(result.current.results?.nodes[0].id).toBe("new-first"),
    );
    expect(mocks.search).toHaveBeenLastCalledWith(
      expect.objectContaining({
        currentNodeId: "folder-2",
        page: 1,
      }),
    );
    expect(oldSignal?.aborted).toBe(true);

    await act(async () => finishOldPage(response("old-second")));
    expect(result.current.results?.nodes.map((node) => node.id)).toEqual([
      "new-first",
    ]);
  });
});
