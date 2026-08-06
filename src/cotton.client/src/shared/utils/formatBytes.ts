const BINARY_UNITS = ["B", "KB", "MB", "GB", "TB", "PB"] as const;
const DEFAULT_MAX_UNIT_INDEX = BINARY_UNITS.indexOf("TB");
const STORAGE_MAX_UNIT_INDEX = BINARY_UNITS.indexOf("PB");

interface ScaledBytes {
  value: number;
  unitIndex: number;
}

const scaleBytes = (bytes: number, maxUnitIndex: number): ScaledBytes => {
  let value = bytes;
  let unitIndex = 0;

  while (value >= 1024 && unitIndex < maxUnitIndex) {
    value /= 1024;
    unitIndex += 1;
  }

  return { value, unitIndex };
};

export const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return "0 B";
  }

  const { value, unitIndex } = scaleBytes(bytes, DEFAULT_MAX_UNIT_INDEX);
  const precision = unitIndex === 0 ? 0 : value < 10 ? 2 : 1;
  return `${value.toFixed(precision)} ${BINARY_UNITS[unitIndex]}`;
};

export const formatStorageBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return "0 B";
  }

  const { value, unitIndex } = scaleBytes(bytes, STORAGE_MAX_UNIT_INDEX);
  const fractionDigits = unitIndex === 0 ? 0 : 2;
  const formattedValue = new Intl.NumberFormat(undefined, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(value);

  return `${formattedValue} ${BINARY_UNITS[unitIndex]}`;
};
