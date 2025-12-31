/**
 * Split a filename into base name and extension.
 * Examples:
 *   "photo.jpg" -> { base: "photo", ext: ".jpg" }
 *   "archive.tar.gz" -> { base: "archive.tar", ext: ".gz" }
 *   "README" -> { base: "README", ext: "" }
 */
export function splitFileName(name: string): { base: string; ext: string } {
  const lastDot = name.lastIndexOf(".");
  if (lastDot <= 0) {
    return { base: name, ext: "" };
  }
  return { base: name.slice(0, lastDot), ext: name.slice(lastDot) };
}

/**
 * Parse a filename to extract the base name and any existing (N) counter.
 * Examples:
 *   "photo.jpg" -> { baseName: "photo", counter: null, ext: ".jpg" }
 *   "photo (3).jpg" -> { baseName: "photo", counter: 3, ext: ".jpg" }
 *   "file (1) (2).txt" -> { baseName: "file (1)", counter: 2, ext: ".txt" }
 */
export function parseFileName(name: string): {
  baseName: string;
  counter: number | null;
  ext: string;
} {
  const { base, ext } = splitFileName(name);

  // Match trailing " (N)" pattern where N is a positive integer
  const match = base.match(/^(.*)\s\((\d+)\)$/);
  if (!match) {
    return { baseName: base, counter: null, ext };
  }

  const counterValue = parseInt(match[2], 10);
  if (!Number.isFinite(counterValue) || counterValue < 1) {
    return { baseName: base, counter: null, ext };
  }

  return {
    baseName: match[1],
    counter: counterValue,
    ext,
  };
}

/**
 * Generate the next available filename given a set of taken names (case-insensitive).
 * If the original name is free, returns it unchanged.
 * Otherwise, finds the next available "baseName (N).ext" where N starts from 1
 * (or increments from an existing counter).
 *
 * Examples:
 *   "photo.jpg" (not taken) -> "photo.jpg"
 *   "photo.jpg" (taken) -> "photo (1).jpg"
 *   "photo (3).jpg" (taken, and "photo (4).jpg" is free) -> "photo (4).jpg"
 */
export function nextAvailableName(
  originalName: string,
  takenLower: Set<string>,
): string {
  const originalLower = originalName.toLowerCase();
  if (!takenLower.has(originalLower)) {
    return originalName;
  }

  const { baseName, counter, ext } = parseFileName(originalName);

  // Start searching from the next counter value (or 1 if no counter exists)
  const startFrom = counter !== null ? counter + 1 : 1;

  for (let i = startFrom; i < 1_000_000; i += 1) {
    const candidate = `${baseName} (${i})${ext}`;
    if (!takenLower.has(candidate.toLowerCase())) {
      return candidate;
    }
  }

  // Fallback (should never happen in practice)
  return `${baseName} (${Date.now()})${ext}`;
}
