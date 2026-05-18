import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { queryKeys } from "./queryKeys";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

describe("queryKeys prefix matching", () => {
  it("matches all notification keys from the notifications root", () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: false }),
      "all",
    );
    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: true }),
      "unread-list",
    );
    queryClient.setQueryData(queryKeys.notifications.unreadCount(), 2);

    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.notifications.all() }),
    ).toHaveLength(3);
  });

  it("matches every admin user list variant from the users root", () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(
      queryKeys.admin.users.list({ withStorage: false }),
      "fast",
    );
    queryClient.setQueryData(
      queryKeys.admin.users.list({ withStorage: true }),
      "with-storage",
    );

    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.admin.users.all() }),
    ).toHaveLength(2);
  });

  it("matches every GC timeline range from the GC timeline root", () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(
      queryKeys.admin.gcTimeline.detail({ bucket: "hour" }),
      "hourly",
    );
    queryClient.setQueryData(
      queryKeys.admin.gcTimeline.detail({
        bucket: "day",
        fromUtc: "2026-05-01T00:00:00Z",
        toUtc: "2026-05-02T00:00:00Z",
      }),
      "daily-range",
    );

    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.admin.gcTimeline.all() }),
    ).toHaveLength(2);
  });

  it("matches trash children pages for one node without touching another node", () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(
      queryKeys.trash.children.page("node-a", {
        page: 1,
        pageSize: 100,
        depth: 1,
      }),
      "a-page-1",
    );
    queryClient.setQueryData(
      queryKeys.trash.children.page("node-a", {
        page: 2,
        pageSize: 100,
        depth: 1,
      }),
      "a-page-2",
    );
    queryClient.setQueryData(
      queryKeys.trash.children.page("node-b", {
        page: 1,
        pageSize: 100,
        depth: 1,
      }),
      "b-page-1",
    );

    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.trash.children.all("node-a") }),
    ).toHaveLength(2);
    expect(
      queryClient
        .getQueryCache()
        .findAll({ queryKey: queryKeys.trash.children.all("node-b") }),
    ).toHaveLength(1);
  });

  it("removes all trash keys from the trash root", () => {
    const queryClient = createQueryClient();
    queryClient.setQueryData(queryKeys.trash.root(), "root");
    queryClient.setQueryData(queryKeys.trash.meta("node-a"), "meta");
    queryClient.setQueryData(
      queryKeys.trash.children.page("node-a", {
        page: 1,
        pageSize: 100,
        depth: 1,
      }),
      "children",
    );

    queryClient.removeQueries({ queryKey: queryKeys.trash.all() });

    expect(
      queryClient.getQueryCache().findAll({ queryKey: queryKeys.trash.all() }),
    ).toEqual([]);
  });
});

describe("queryKeys shape stability", () => {
  it("keeps layout and server settings key shapes stable", () => {
    expect(queryKeys.layouts.recent("layout-1", 5)).toEqual([
      "layouts",
      "recent",
      "layout-1",
      5,
    ]);
    expect(queryKeys.serverSettings.all()).toEqual(["serverSettings"]);
  });

  it("keeps admin variants encoded in object payloads", () => {
    expect(queryKeys.admin.users.list({ withStorage: false })).toEqual([
      "admin",
      "users",
      "list",
      { withStorage: false },
    ]);
    expect(queryKeys.admin.gcTimeline.detail({ bucket: "day" })).toEqual([
      "admin",
      "gcTimeline",
      { bucket: "day" },
    ]);
  });

  it("keeps audio and trash keys scoped by their inputs", () => {
    expect(
      queryKeys.audio.trackLyrics({
        folderNodeId: "folder-1",
        trackName: "Track",
      }),
    ).toEqual([
      "audio",
      "trackLyrics",
      { folderNodeId: "folder-1", trackName: "Track" },
    ]);
    expect(
      queryKeys.trash.children.page("node-1", {
        page: 3,
        pageSize: 50,
        depth: 2,
      }),
    ).toEqual([
      "trash",
      "children",
      "node-1",
      { page: 3, pageSize: 50, depth: 2 },
    ]);
  });
});
