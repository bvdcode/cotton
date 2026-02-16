export function tryParseFileName(
  contentDisposition: string | null,
): string | null {
  if (!contentDisposition) return null;

  // RFC 5987 / RFC 6266: prefer `filename*` over `filename`.
  // Example: inline; filename*=UTF-8''20250903_130511.heic; filename=20250903_130511.heic
  const filenameStarMatch = contentDisposition.match(
    /filename\*\s*=\s*([^']+)''([^;]+)/i,
  );
  if (filenameStarMatch?.[2]) {
    const value = filenameStarMatch[2].trim();
    try {
      return decodeURIComponent(value);
    } catch {
      return value;
    }
  }

  const filenameMatch = contentDisposition.match(/filename\s*=\s*([^;]+)/i);
  if (filenameMatch?.[1]) {
    const value = filenameMatch[1].trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      return value.slice(1, -1);
    }
    return value;
  }

  return null;
}
