export const readStringProperty = <T>(
  value: T,
  property: string,
): string | null => {
  if (typeof value !== "object" || value === null) {
    return null;
  }
  const propertyValue = Object.entries(value).find(([key]) => key === property)?.[1];
  return typeof propertyValue === "string" ? propertyValue : null;
};

export const readObjectProperty = <T>(
  value: T,
  property: string,
): object | null => {
  if (typeof value !== "object" || value === null) {
    return null;
  }
  const propertyValue = Object.entries(value).find(([key]) => key === property)?.[1];
  return typeof propertyValue === "object" && propertyValue !== null
    ? propertyValue
    : null;
};
