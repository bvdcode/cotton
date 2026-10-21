import { act, renderHook, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { generateMasterKey } from "./keys";
import { useDisplayProjection } from "./useDisplayProjection";
import { useVault } from "./vault";

afterEach(() => useVault.getState().lock());

function pendingProjection() {
  let resolve!: (value: string) => void;
  const promise = new Promise<string>((complete) => {
    resolve = complete;
  });
  return { promise, resolve };
}

it("hides decoded values immediately on lock and projects again on unlock", async () => {
  const key = await generateMasterKey();
  useVault.getState().unlock(key, { persistToSession: false });
  const project = vi.fn(async () => "Private folder");
  const { result } = renderHook(() => useDisplayProjection("opaque", project));

  await waitFor(() => expect(result.current).toBe("Private folder"));
  act(() => useVault.getState().lock());
  expect(result.current).toBe("opaque");
  act(() => useVault.getState().unlock(key, { persistToSession: false }));
  await waitFor(() => expect(result.current).toBe("Private folder"));
  expect(project).toHaveBeenCalledTimes(2);
});

it("does not publish a projection completed after the vault locks", async () => {
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false });
  const pending = pendingProjection();
  const project = vi.fn(() => pending.promise);
  const { result } = renderHook(() => useDisplayProjection("opaque", project));

  act(() => useVault.getState().lock());
  await act(async () => pending.resolve("Private folder"));
  expect(result.current).toBe("opaque");
});

it("discards a stale projection after the source changes", async () => {
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false });
  const oldProjection = pendingProjection();
  const project = vi.fn((value: string) =>
    value === "old" ? oldProjection.promise : Promise.resolve("New folder"),
  );
  const { result, rerender } = renderHook(
    ({ source }) => useDisplayProjection(source, project),
    {
      initialProps: { source: "old" },
    },
  );

  rerender({ source: "new" });
  await waitFor(() => expect(result.current).toBe("New folder"));
  await act(async () => oldProjection.resolve("Old folder"));
  expect(result.current).toBe("New folder");
});

it("does not reuse a projection across encryption keys", async () => {
  useVault
    .getState()
    .unlock(await generateMasterKey(), { persistToSession: false });
  const nextKey = await generateMasterKey();
  const pending = pendingProjection();
  const project = vi
    .fn()
    .mockResolvedValueOnce("Old private folder")
    .mockReturnValueOnce(pending.promise);
  const { result } = renderHook(() => useDisplayProjection("opaque", project));
  await waitFor(() => expect(result.current).toBe("Old private folder"));

  act(() => useVault.getState().unlock(nextKey, { persistToSession: false }));
  expect(result.current).toBe("opaque");
  await act(async () => pending.resolve("New private folder"));
  expect(result.current).toBe("New private folder");
});
