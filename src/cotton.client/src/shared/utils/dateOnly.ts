const DATE_PREFIX_RE = /^(\d{4})-(\d{2})-(\d{2})/;

export const toDateInputValue = (
  value: string | null | undefined,
): string => {
  if (!value) return "";

  const trimmed = value.trim();
  if (trimmed.length === 0) return "";

  const match = DATE_PREFIX_RE.exec(trimmed);
  if (!match) return "";

  return `${match[1]}-${match[2]}-${match[3]}`;
};

export const tryParseDateOnly = (value: string): Date | null => {
  const trimmed = value.trim();
  const match = DATE_PREFIX_RE.exec(trimmed);
  if (!match) return null;

  const year = Number(match[1]);
  const monthIndex = Number(match[2]) - 1;
  const day = Number(match[3]);

  if (!Number.isFinite(year) || !Number.isFinite(monthIndex) || !Number.isFinite(day)) {
    return null;
  }
  if (monthIndex < 0 || monthIndex > 11 || day < 1 || day > 31) {
    return null;
  }

  const date = new Date(year, monthIndex, day);
  if (
    date.getFullYear() !== year ||
    date.getMonth() !== monthIndex ||
    date.getDate() !== day
  ) {
    return null;
  }

  return date;
};

export const formatDateOnly = (value: string): string => {
  const parsedDateOnly = tryParseDateOnly(value);
  if (parsedDateOnly) {
    return new Intl.DateTimeFormat(undefined, {
      year: "numeric",
      month: "short",
      day: "2-digit",
    }).format(parsedDateOnly);
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
  }).format(parsed);
};

export const getAgeYears = (birthDate: Date, now: Date = new Date()): number => {
  let age = now.getFullYear() - birthDate.getFullYear();

  const monthDelta = now.getMonth() - birthDate.getMonth();
  if (monthDelta < 0 || (monthDelta === 0 && now.getDate() < birthDate.getDate())) {
    age -= 1;
  }

  return age;
};
