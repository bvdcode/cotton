export type DirectoryField = "Name" | "SizeBytes";
export type DirectoryFilterOperator =
  | "Contains"
  | "DoesNotContain"
  | "Equals"
  | "DoesNotEqual"
  | "StartsWith"
  | "EndsWith"
  | "IsEmpty"
  | "IsNotEmpty"
  | "IsAnyOf"
  | "GreaterThan"
  | "GreaterThanOrEqual"
  | "LessThan"
  | "LessThanOrEqual";

export interface DirectoryListingOptions {
  sortBy?: DirectoryField;
  descending?: boolean;
  filterBy?: DirectoryField;
  filterOperator?: DirectoryFilterOperator;
  filterValue?: string;
  filterValues?: string[];
}
