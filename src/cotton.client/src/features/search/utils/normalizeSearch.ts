import type { LayoutSearchResultDto } from "../../../shared/api/layoutsApi";
import type { SearchDictionaryEntry } from "../types";
import { isJsonObject, type JsonValue } from "../../../shared/types/json";

export const normalizeSearchText = (value: string): string =>
  value.toLocaleLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");

export const normalizeCompactSearchText = (value: string): string =>
  normalizeSearchText(value).replace(/[\s._\-/:\\]+/g, "");

export const isDictionaryEntry = (
  value: JsonValue,
): value is SearchDictionaryEntry & JsonValue => {
  if (!isJsonObject(value)) return false;
  const keywords = value.keywords;

  return (
    typeof value.id === "string" &&
    typeof value.title === "string" &&
    typeof value.path === "string" &&
    Array.isArray(keywords) &&
    keywords.every((keyword) => typeof keyword === "string") &&
    (value.description === undefined ||
      typeof value.description === "string") &&
    (value.highlightSettingId === undefined ||
      typeof value.highlightSettingId === "string") &&
    (value.adminOnly === undefined || typeof value.adminOnly === "boolean")
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
