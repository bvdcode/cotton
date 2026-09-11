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
      queryKeys.layouts.recentFiltered("layout-id", 15, ["image/*"], [], false),
      [],
    );

    clearLayoutsCaches(queryClient);

    expect(queryClient.getQueryData(queryKeys.layouts.root())).toBeUndefined();
    expect(
      queryClient.getQueryData(queryKeys.layouts.stats("layout-id")),
    ).toBeUndefined();
    expect(
      queryClient.getQueryData(
        queryKeys.layouts.recentFiltered(
          "layout-id",
          15,
          ["image/*"],
          [],
          false,
        ),
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
      queryKeys.layouts.recentFiltered("layout-1", 5, ["image/*"], [], false),
      [],
    );
    queryClient.setQueryData(
      queryKeys.layouts.recentFiltered("layout-1", 15, [], ["image/*"], false),
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
        queryKeys.layouts.recentFiltered("layout-1", 5, ["image/*"], [], false),
      )?.isInvalidated,
    ).toBe(true);
    expect(
      queryClient.getQueryState(
        queryKeys.layouts.recentFiltered(
          "layout-1",
          15,
          [],
          ["image/*"],
          false,
        ),
      )?.isInvalidated,
    ).toBe(true);
    expect(
      queryClient.getQueryState(queryKeys.layouts.stats("layout-2"))
        ?.isInvalidated,
    ).toBe(false);
  });

  it("invalidates quota and every pinned-folder selection with the overview", async () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(queryKeys.storageQuota.current(), {
      usedBytes: 10,
    });
    queryClient.setQueryData(queryKeys.layouts.pinnedFolders(["node-a"]), []);
    queryClient.setQueryData(queryKeys.layouts.pinnedFolders(["node-b"]), []);

    await invalidateLayoutOverview(queryClient, "layout-1");

    expect(
      queryClient.getQueryState(queryKeys.storageQuota.current())
        ?.isInvalidated,
    ).toBe(true);
    expect(
      queryClient.getQueryState(queryKeys.layouts.pinnedFolders(["node-a"]))
        ?.isInvalidated,
    ).toBe(true);
    expect(
      queryClient.getQueryState(queryKeys.layouts.pinnedFolders(["node-b"]))
        ?.isInvalidated,
    ).toBe(true);
  });
});
