export const USERNAME_MIN_LENGTH = 2;
export const USERNAME_MAX_LENGTH = 32;

// Must start with a letter; only lowercase latin letters and digits; total length 2..32
export const USERNAME_REGEX = /^[a-z][a-z0-9]{1,31}$/;

export const normalizeUsername = (value: string): string =>
  value.trim().toLowerCase();

export const isValidUsername = (value: string): boolean =>
  USERNAME_REGEX.test(normalizeUsername(value));

export const getUsernameError = (value: string): string | null => {
  const normalized = normalizeUsername(value);
  if (normalized.length === 0) return null;

  if (normalized.length < USERNAME_MIN_LENGTH || normalized.length > USERNAME_MAX_LENGTH) {
    return `Username must be between ${USERNAME_MIN_LENGTH} and ${USERNAME_MAX_LENGTH} characters.`;
  }

  if (!USERNAME_REGEX.test(normalized)) {
    return "Username must start with a letter and contain only lowercase latin letters and digits.";
  }

  return null;
};
