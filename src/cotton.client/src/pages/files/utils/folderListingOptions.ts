import type { GridFilterModel, GridSortModel } from "@mui/x-data-grid";
import { z } from "zod";
import type {
  DirectoryField,
  DirectoryFilterOperator,
  DirectoryListingOptions,
} from "../../../shared/api/types/DirectoryListingOptions";

function field(value: string): DirectoryField {
  switch (value) {
    case "name":
      return "Name";
    case "sizeBytes":
      return "SizeBytes";
    default:
      throw new Error(`Unsupported directory field: ${value}`);
  }
}

function operator(value: string): DirectoryFilterOperator {
  switch (value) {
    case "contains":
      return "Contains";
    case "doesNotContain":
      return "DoesNotContain";
    case "equals":
    case "=":
      return "Equals";
    case "doesNotEqual":
    case "!=":
      return "DoesNotEqual";
    case "startsWith":
      return "StartsWith";
    case "endsWith":
      return "EndsWith";
    case "isEmpty":
      return "IsEmpty";
    case "isNotEmpty":
      return "IsNotEmpty";
    case "isAnyOf":
      return "IsAnyOf";
    case ">":
      return "GreaterThan";
    case ">=":
      return "GreaterThanOrEqual";
    case "<":
      return "LessThan";
    case "<=":
      return "LessThanOrEqual";
    default:
      throw new Error(`Unsupported directory filter: ${value}`);
  }
}

const scalar = z
  .union([z.string(), z.number()])
  .transform((value) => String(value).trim());

export function folderListingOptions(
  sort: GridSortModel,
  filter: GridFilterModel,
): DirectoryListingOptions {
  const options: DirectoryListingOptions = {};
  const selectedSort = sort[0];
  if (selectedSort?.sort) {
    options.sortBy = field(selectedSort.field);
    options.descending = selectedSort.sort === "desc";
  }
  const item = filter.items[0];
  if (!item) {
    return options;
  }
  const filterOperator = operator(item.operator);
  if (filterOperator === "IsEmpty" || filterOperator === "IsNotEmpty") {
    options.filterBy = field(item.field);
    options.filterOperator = filterOperator;
    return options;
  }
  if (filterOperator === "IsAnyOf") {
    const parsed = z.array(scalar).safeParse(item.value);
    if (!parsed.success || parsed.data.length === 0) {
      return options;
    }
    options.filterValues = parsed.data;
  } else {
    const parsed = scalar.safeParse(item.value);
    if (!parsed.success || parsed.data.length === 0) {
      return options;
    }
    options.filterValue = parsed.data;
  }
  options.filterBy = field(item.field);
  options.filterOperator = filterOperator;
  return options;
}
