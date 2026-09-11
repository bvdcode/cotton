const MOBILE_RETURN_TARGET = "mobile";
const MOBILE_RETURN_URI = "cotton://authorization-complete";

export const resolveAppCodeReturnUri = (
  returnTarget: string | null,
): string | null => {
  if (returnTarget !== MOBILE_RETURN_TARGET) {
    return null;
  }

  return MOBILE_RETURN_URI;
};

export const returnToAppCodeCaller = (returnTarget: string | null): void => {
  const returnUri = resolveAppCodeReturnUri(returnTarget);
  if (returnUri === null) {
    return;
  }

  window.location.assign(returnUri);
};
