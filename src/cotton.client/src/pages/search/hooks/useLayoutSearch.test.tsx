import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { LayoutSearchResult } from "../../../shared/api/layoutsApi";
import { useLayoutSearch } from "./useLayoutSearch";

const mocks = vi.hoisted(() => ({ search: vi.fn() }));
vi.mock("../../../shared/api/layoutsApi", () => ({
  layoutsApi: { search: mocks.search },
}));

const response = (totalCount = 0): LayoutSearchResult => ({
  data: { nodes: [], files: [], nodePaths: {}, filePaths: {} },
  totalCount,
});
const flush = async () => {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(350);
  });
  await act(async () => {
    await vi.advanceTimersByTimeAsync(350);
  });
};
const setup = () =>
  renderHook(() =>
    useLayoutSearch({ layoutId: "layout-1", initialQuery: "report" }),
  );

describe("useLayoutSearch", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mocks.search.mockImplementation(() => new Promise(() => {}));
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    vi.resetAllMocks();
  });

  it("enables deep search for empty results and honors manual disabling", async () => {
    mocks.search.mockResolvedValue(response());
    const { result } = setup();
    expect(result.current.deep).toBe(false);
    await flush();
    expect(result.current.deep).toBe(true);
    expect(mocks.search).toHaveBeenCalledTimes(2);
    act(() => result.current.toggleDeep());
    await flush();
    expect(result.current.deep).toBe(false);
    expect(mocks.search).toHaveBeenCalledTimes(3);
    act(() => result.current.setQuery("invoice"));
    await flush();
    expect(result.current.deep).toBe(false);
    expect(mocks.search).toHaveBeenCalledTimes(4);
  });

  it("allows selecting deep search before entering a query", async () => {
    mocks.search.mockResolvedValue(response(2));
    const { result } = renderHook(() =>
      useLayoutSearch({ layoutId: "layout-1", initialQuery: "" }),
    );
    act(() => result.current.toggleDeep());
    expect(result.current.deep).toBe(true);
    expect(mocks.search).not.toHaveBeenCalled();
    act(() => result.current.setQuery("report"));
    await flush();
    expect(mocks.search).toHaveBeenCalledWith(
      expect.objectContaining({ query: "report", deep: true }),
    );
  });

  it("resets pagination when switching modes", async () => {
    mocks.search.mockResolvedValue(response(200));
    const { result } = setup();
    await flush();
    act(() => result.current.setPage(3));
    await flush();
    act(() => result.current.toggleDeep());
    expect(result.current.page).toBe(1);
    expect(result.current.loading).toBe(true);
    expect(result.current.results).toBeNull();
    await flush();
    expect(mocks.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 1, deep: true }),
    );
  });

  it("cancels and discards a deep response after changing the query", async () => {
    let resolveDeep: (value: LayoutSearchResult) => void = () => {};
    mocks.search
      .mockResolvedValueOnce(response())
      .mockReturnValueOnce(
        new Promise<LayoutSearchResult>((resolve) => {
          resolveDeep = resolve;
        }),
      )
      .mockResolvedValueOnce(response(2));
    const { result } = setup();
    await flush();
    const signal: AbortSignal = mocks.search.mock.calls[1][0].signal;
    act(() => result.current.setQuery("invoice"));
    expect(signal.aborted).toBe(true);
    await act(async () => resolveDeep(response(20)));
    expect(result.current.deep).toBe(true);
    expect(result.current.results).toBeNull();
    await flush();
    expect(result.current.totalCount).toBe(2);
    expect(result.current.completedQuery).toBe("invoice");
  });

  it("shows deep failure without stale ordinary results or automatic retries", async () => {
    mocks.search
      .mockResolvedValueOnce(response())
      .mockRejectedValueOnce(new Error("worker down"));
    const { result } = setup();
    await flush();
    expect(result.current.error).toBe("smartSearch.error");
    expect(result.current.loading).toBe(false);
    expect(result.current.results).toBeNull();
    expect(mocks.search).toHaveBeenCalledTimes(2);
  });
});
