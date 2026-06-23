import { describe, expect, it } from "vitest";
import {
  SEARCH_HISTORY_LIMIT,
  addSearchHistoryEntry,
  parseSearchHistoryPreference,
  removeSearchHistoryEntry,
  serializeSearchHistoryPreference,
} from "./searchHistory";

describe("searchHistory", () => {
  it("parses valid entries and ignores invalid preference values", () => {
    expect(parseSearchHistoryPreference(undefined)).toEqual([]);
    expect(parseSearchHistoryPreference("{bad json")).toEqual([]);
    expect(parseSearchHistoryPreference('"not-array"')).toEqual([]);

    expect(
      parseSearchHistoryPreference(
        JSON.stringify([
          {
            query: "  quarterly   report ",
            lastUsedAt: "2026-06-09T00:00:00.000Z",
          },
          { query: "" },
          { query: "quarterly report", lastUsedAt: "duplicate" },
          " photos ",
        ]),
      ),
    ).toEqual([
      {
        query: "quarterly report",
        lastUsedAt: "2026-06-09T00:00:00.000Z",
      },
      { query: "photos", lastUsedAt: "" },
    ]);
  });

  it("adds normalized queries to the top and deduplicates case-insensitively", () => {
    const first = addSearchHistoryEntry(
      [],
      " Project   plan ",
      new Date("2026-06-09T00:00:00.000Z"),
    );
    const next = addSearchHistoryEntry(
      first,
      "project plan",
      new Date("2026-06-10T00:00:00.000Z"),
    );

    expect(next).toEqual([
      {
        query: "project plan",
        lastUsedAt: "2026-06-10T00:00:00.000Z",
      },
    ]);
  });

  it("keeps only the newest limited set of entries", () => {
    const entries = Array.from(
      { length: SEARCH_HISTORY_LIMIT + 2 },
      (_, index) => ({
        query: `query ${index}`,
        lastUsedAt: "2026-06-09T00:00:00.000Z",
      }),
    );

    const next = addSearchHistoryEntry(
      entries,
      "latest",
      new Date("2026-06-10T00:00:00.000Z"),
    );

    expect(next).toHaveLength(SEARCH_HISTORY_LIMIT);
    expect(next[0]?.query).toBe("latest");
    expect(next.at(-1)?.query).toBe(`query ${SEARCH_HISTORY_LIMIT - 2}`);
  });

  it("removes entries by normalized query", () => {
    expect(
      removeSearchHistoryEntry(
        [
          { query: "Photos", lastUsedAt: "2026-06-09T00:00:00.000Z" },
          { query: "documents", lastUsedAt: "2026-06-09T00:00:00.000Z" },
        ],
        " photos ",
      ),
    ).toEqual([{ query: "documents", lastUsedAt: "2026-06-09T00:00:00.000Z" }]);
  });

  it("serializes the limited preference payload", () => {
    const entries = Array.from(
      { length: SEARCH_HISTORY_LIMIT + 1 },
      (_, index) => ({
        query: `query ${index}`,
        lastUsedAt: "2026-06-09T00:00:00.000Z",
      }),
    );

    expect(JSON.parse(serializeSearchHistoryPreference(entries))).toHaveLength(
      SEARCH_HISTORY_LIMIT,
    );
  });
});
