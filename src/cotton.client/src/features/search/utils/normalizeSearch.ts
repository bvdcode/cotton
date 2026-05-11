import type { LayoutSearchResultDto } from "../../../shared/api/layoutsApi";
import type { SearchDictionaryEntry } from "../types";

export const normalizeSearchText = (value: string): string =>
  value
    .toLocaleLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "");

export const normalizeCompactSearchText = (value: string): string =>
  normalizeSearchText(value).replace(/[\s._\-/:\\]+/g, "");

export const isDictionaryEntry = (
  value: unknown,
): value is SearchDictionaryEntry => {
  if (!value || typeof value !== "object") return false;

  const record = value as Record<string, unknown>;
  const keywords = record.keywords;

  return (
    typeof record.id === "string" &&
    typeof record.title === "string" &&
    typeof record.path === "string" &&
    Array.isArray(keywords) &&
    keywords.every((keyword) => typeof keyword === "string") &&
    (record.description === undefined ||
      typeof record.description === "string") &&
    (record.highlightSettingId === undefined ||
      typeof record.highlightSettingId === "string") &&
    (record.adminOnly === undefined || typeof record.adminOnly === "boolean")
  );
};

export const mergeSearchResults = (
  previous: LayoutSearchResultDto | null,
  next: LayoutSearchResultDto,
): LayoutSearchResultDto => {
  if (!previous) return next;

  return {
    nodes: [...(previous.nodes ?? []), ...(next.nodes ?? [])],
    files: [...(previous.files ?? []), ...(next.files ?? [])],
    nodePaths: {
      ...(previous.nodePaths ?? {}),
      ...(next.nodePaths ?? {}),
    },
    filePaths: {
      ...(previous.filePaths ?? {}),
      ...(next.filePaths ?? {}),
    },
  };
};
