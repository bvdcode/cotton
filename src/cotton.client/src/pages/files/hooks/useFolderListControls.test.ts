import { act, renderHook } from "@testing-library/react";
import { expect, it } from "vitest";
import { useFolderListControls } from "./useFolderListControls";

it("keeps the page and non-name ordering when the encryption check allows names", () => {
  const { result, rerender } = renderHook(
    ({ namesEnabled }) => useFolderListControls("folder", namesEnabled),
    { initialProps: { namesEnabled: false } },
  );
  act(() =>
    result.current.query.onSortModelChange([
      { field: "sizeBytes", sort: "desc" },
    ]),
  );
  act(() => result.current.setPage(3));
  rerender({ namesEnabled: true });
  expect(result.current.page).toBe(3);
  expect(result.current.listing).toEqual({
    sortBy: "SizeBytes",
    descending: true,
  });
  expect(result.current.query.namesEnabled).toBe(true);
});

it("resets a name query when encryption disables name operations", () => {
  const { result, rerender } = renderHook(
    ({ namesEnabled }) => useFolderListControls("folder", namesEnabled),
    { initialProps: { namesEnabled: true } },
  );
  act(() =>
    result.current.query.onSortModelChange([{ field: "name", sort: "desc" }]),
  );
  act(() => result.current.setPage(3));
  rerender({ namesEnabled: false });
  expect(result.current.page).toBe(0);
  expect(result.current.listing).toEqual({});
  expect(result.current.query.namesEnabled).toBe(false);
});
