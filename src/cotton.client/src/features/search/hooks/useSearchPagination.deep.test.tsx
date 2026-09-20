import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { LayoutSearchResult } from "../../../shared/api/layoutsApi";
import { useSearchPagination } from "./useSearchPagination";

const mocks = vi.hoisted(() => ({ search: vi.fn() }));
vi.mock("../../../shared/api/layoutsApi", () => ({
  layoutsApi: { search: mocks.search },
}));

const response = (totalCount = 0): LayoutSearchResult => ({
  data: { nodes: [], files: [], nodePaths: {}, filePaths: {} },
  totalCount,
});

const flush = () =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(300);
  });
const setup = () =>
  renderHook(
    ({ query, layoutId }) =>
      useSearchPagination({ trimmedQuery: query, layoutId }),
    { initialProps: { query: "report", layoutId: "layout-1" } },
  );

describe("deep search pagination", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mocks.search.mockImplementation(() => new Promise(() => {}));
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    vi.resetAllMocks();
  });

  it("starts with ordinary search and enables deep search only after an empty response", async () => {
    mocks.search
      .mockResolvedValueOnce(response())
      .mockResolvedValueOnce(response(2));
    const { result } = setup();
    expect(result.current.deep).toBe(false);
    await flush();
    expect(mocks.search).toHaveBeenCalledTimes(2);
    expect(mocks.search).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ deep: false, page: 1 }),
    );
    expect(mocks.search).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ deep: true, page: 1 }),
    );
    expect(result.current.deep).toBe(true);
    expect(result.current.totalCount).toBe(2);
  });

  it("keeps ordinary results without requesting embeddings", async () => {
    mocks.search.mockResolvedValue(response(1));
    const { result } = setup();
    await flush();
    expect(mocks.search).toHaveBeenCalledTimes(1);
    expect(result.current.deep).toBe(false);
  });

  it("does not enable deep search after an ordinary request fails", async () => {
    mocks.search.mockRejectedValueOnce(new Error("unavailable"));
    const { result } = setup();
    await flush();
    expect(mocks.search).toHaveBeenCalledTimes(1);
    expect(result.current.deep).toBe(false);
    expect(result.current.error).toBe("error");
  });

  it("keeps manual disabling while the query changes", async () => {
    mocks.search.mockResolvedValue(response());
    const { result, rerender } = setup();
    await flush();
    expect(result.current.deep).toBe(true);
    act(() => result.current.toggleDeep());
    await flush();
    expect(result.current.deep).toBe(false);
    expect(mocks.search).toHaveBeenCalledTimes(3);
    rerender({ query: "invoice", layoutId: "layout-1" });
    await flush();
    expect(result.current.deep).toBe(false);
    expect(mocks.search).toHaveBeenCalledTimes(4);
  });

  it("allows selecting deep search before entering a query", async () => {
    mocks.search.mockResolvedValue(response(2));
    const { result, rerender } = renderHook(
      ({ query }) =>
        useSearchPagination({ trimmedQuery: query, layoutId: "layout-1" }),
      { initialProps: { query: "" } },
    );
    act(() => result.current.toggleDeep());
    expect(result.current.deep).toBe(true);
    expect(mocks.search).not.toHaveBeenCalled();
    rerender({ query: "report" });
    await flush();
    expect(mocks.search).toHaveBeenCalledWith(
      expect.objectContaining({ query: "report", deep: true }),
    );
  });

  it("discards a pending deep response after manual disabling", async () => {
    let resolveDeep: (value: LayoutSearchResult) => void = () => {};
    const pendingDeep = new Promise<LayoutSearchResult>((resolve) => {
      resolveDeep = resolve;
    });
    mocks.search
      .mockResolvedValueOnce(response())
      .mockReturnValueOnce(pendingDeep)
      .mockResolvedValueOnce(response());
    const { result } = setup();
    await flush();
    const deepSignal: AbortSignal = mocks.search.mock.calls[1][0].signal;
    act(() => result.current.toggleDeep());
    await flush();
    expect(deepSignal.aborted).toBe(true);
    await act(async () => resolveDeep(response(10)));
    expect(result.current.deep).toBe(false);
    expect(result.current.totalCount).toBe(0);
    expect(mocks.search).toHaveBeenCalledTimes(3);
  });

  it("ignores an empty answer from the previous query during the debounce delay", async () => {
    let resolveOld: (value: LayoutSearchResult) => void = () => {};
    mocks.search.mockReturnValueOnce(
      new Promise<LayoutSearchResult>((resolve) => {
        resolveOld = resolve;
      }),
    );
    mocks.search.mockResolvedValueOnce(response(3));
    const { result, rerender } = setup();
    rerender({ query: "invoice", layoutId: "layout-1" });
    await act(async () => resolveOld(response()));
    expect(result.current.deep).toBe(false);
    await flush();
    expect(result.current.totalCount).toBe(3);
    expect(mocks.search).toHaveBeenCalledTimes(2);
  });

  it("keeps deep mode on later pages and restarts at page one after toggling", async () => {
    mocks.search
      .mockResolvedValueOnce(response())
      .mockResolvedValue(response(150));
    const { result } = setup();
    await flush();
    act(() => result.current.loadNextPage());
    await flush();
    expect(mocks.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ deep: true, page: 2 }),
    );
    act(() => result.current.toggleDeep());
    await flush();
    expect(mocks.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ deep: false, page: 1 }),
    );
  });

  it("cancels requests on clearing or unmounting the search", async () => {
    const { result, rerender, unmount } = setup();
    const signal: AbortSignal = mocks.search.mock.calls[0][0].signal;
    rerender({ query: "", layoutId: "layout-1" });
    expect(signal.aborted).toBe(true);
    expect(result.current.deep).toBe(false);
    expect(result.current.results).toBeNull();
    rerender({ query: "invoice", layoutId: "layout-1" });
    await flush();
    const nextSignal: AbortSignal = mocks.search.mock.calls[1][0].signal;
    unmount();
    expect(nextSignal.aborted).toBe(true);
  });

  it("does not retry a failed deep request or hide the failure", async () => {
    mocks.search
      .mockResolvedValueOnce(response())
      .mockRejectedValueOnce(new Error("worker down"));
    const { result } = setup();
    await flush();
    expect(result.current.deep).toBe(true);
    expect(result.current.error).toBe("smartSearch.error");
    expect(result.current.loadingInitial).toBe(false);
    expect(mocks.search).toHaveBeenCalledTimes(2);
  });
});
