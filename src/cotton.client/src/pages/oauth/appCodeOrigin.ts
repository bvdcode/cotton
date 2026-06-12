const loopbackIpv4Pattern = /^127(?:\.\d{1,3}){3}$/;
const ipv4MappedLoopbackPattern = /^::ffff:127(?:\.\d{1,3}){3}$/i;

export const isLoopbackOrigin = (origin: string): boolean => {
  const normalized = origin.trim().toLowerCase();

  return (
    normalized === "localhost" ||
    normalized === "::1" ||
    loopbackIpv4Pattern.test(normalized) ||
    ipv4MappedLoopbackPattern.test(normalized)
  );
};

export const formatAppCodeOrigin = (
  origin: string,
  localOriginLabel: string,
): string => {
  if (isLoopbackOrigin(origin)) {
    return localOriginLabel;
  }

  return origin;
};
