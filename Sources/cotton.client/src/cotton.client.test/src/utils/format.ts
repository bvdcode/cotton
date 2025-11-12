export function formatBytes(bytes: number, decimals = 1): string {
  if (!Number.isFinite(bytes)) return "-";
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB", "PB"] as const;
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  const value = bytes / Math.pow(k, i);
  return `${value.toFixed(value < 10 ? decimals : 0)} ${sizes[i]}`;
}

export function formatBytesPerSecond(bps: number, decimals = 1): string {
  return formatBytes(bps, decimals);
}
