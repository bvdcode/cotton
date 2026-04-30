export const APP_TITLE = "Cotton Cloud";

const LEGACY_APP_TITLE = "Cotton";
const TITLE_SEPARATOR = " - ";

const normalizeTitlePart = (title: string | null | undefined): string | null => {
  const normalized = title?.trim();
  return normalized ? normalized : null;
};

export const formatPageTitle = (title: string | null | undefined): string => {
  const normalized = normalizeTitlePart(title);

  if (!normalized || normalized === APP_TITLE) {
    return APP_TITLE;
  }

  const currentPrefix = `${APP_TITLE}${TITLE_SEPARATOR}`;
  if (normalized.startsWith(currentPrefix)) {
    return normalized;
  }

  const legacyPrefix = `${LEGACY_APP_TITLE}${TITLE_SEPARATOR}`;
  if (normalized.startsWith(legacyPrefix)) {
    return `${APP_TITLE}${TITLE_SEPARATOR}${normalized.slice(legacyPrefix.length)}`;
  }

  return `${APP_TITLE}${TITLE_SEPARATOR}${normalized}`;
};

export const setPageTitle = (title?: string | null): void => {
  if (typeof document === "undefined") return;

  document.title = formatPageTitle(title);
};
