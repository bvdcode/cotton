export const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

export const readStringProperty = (
  value: unknown,
  property: string,
): string | null => {
  if (!isRecord(value)) return null;
  const propertyValue = value[property];
  return typeof propertyValue === "string" ? propertyValue : null;
};
