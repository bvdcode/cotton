import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { clearLayoutsCaches, invalidateLayoutOverview } from "./layouts";
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
    queryClient.setQueryData(
      queryKeys.layouts.recentFiltered("layout-id", 15, ["image/*"], []),
      [],
    );

    clearLayoutsCaches(queryClient);

    expect(queryClient.getQueryData(queryKeys.layouts.root())).toBeUndefined();
    expect(
      queryClient.getQueryData(queryKeys.layouts.stats("layout-id")),
    ).toBeUndefined();
    expect(
      queryClient.getQueryData(
        queryKeys.layouts.recentFiltered("layout-id", 15, ["image/*"], []),
      ),
    ).toBeUndefined();
  });

  it("leaves caches in other domains untouched", () => {
    const queryClient = createQueryClient();

    queryClient.setQueryData(queryKeys.layouts.root(), { id: "root" });
    queryClient.setQueryData(queryKeys.notifications.unreadCount(), 3);

    clearLayoutsCaches(queryClient);

    expect(queryClient.getQueryData(queryKeys.layouts.root())).toBeUndefined();
    expect(
      queryClient.getQueryData(queryKeys.notifications.unreadCount()),
    ).toBe(3);
  });

  it("invalidates stats and every recent-file count for one layout", async () => {
    const queryClient = createQueryClient();

    queryClient.setQueryData(queryKeys.layouts.stats("layout-1"), {
      fileCount: 1,
    });
    queryClient.setQueryData(
      queryKeys.layouts.recentFiltered("layout-1", 5, ["image/*"], []),
      [],
    );
    queryClient.setQueryData(
      queryKeys.layouts.recentFiltered("layout-1", 15, [], ["image/*"]),
      [],
    );
    queryClient.setQueryData(queryKeys.layouts.stats("layout-2"), {
      fileCount: 2,
    });

    await invalidateLayoutOverview(queryClient, "layout-1");

    expect(
      queryClient.getQueryState(queryKeys.layouts.stats("layout-1"))
        ?.isInvalidated,
    ).toBe(true);
    expect(
      queryClient.getQueryState(
        queryKeys.layouts.recentFiltered("layout-1", 5, ["image/*"], []),
      )
        ?.isInvalidated,
    ).toBe(true);
    expect(
      queryClient.getQueryState(
        queryKeys.layouts.recentFiltered("layout-1", 15, [], ["image/*"]),
      )
        ?.isInvalidated,
    ).toBe(true);
    expect(
      queryClient.getQueryState(queryKeys.layouts.stats("layout-2"))
        ?.isInvalidated,
    ).toBe(false);
  });
});
