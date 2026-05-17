import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { clearLayoutsCaches } from "./layouts";
import { queryKeys } from "./queryKeys";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

describe("layout query cache helpers", () => {
  it("clears all layout query caches", () => {
    const queryClient = createQueryClient();

    queryClient.setQueryData(queryKeys.layouts.root(), { id: "root" });
    queryClient.setQueryData(queryKeys.layouts.stats("layout-id"), {
      fileCount: 1,
    });
    queryClient.setQueryData(queryKeys.layouts.recent("layout-id", 15), []);

    clearLayoutsCaches(queryClient);

    expect(queryClient.getQueryData(queryKeys.layouts.root())).toBeUndefined();
    expect(
      queryClient.getQueryData(queryKeys.layouts.stats("layout-id")),
    ).toBeUndefined();
    expect(
      queryClient.getQueryData(queryKeys.layouts.recent("layout-id", 15)),
    ).toBeUndefined();
  });
});
