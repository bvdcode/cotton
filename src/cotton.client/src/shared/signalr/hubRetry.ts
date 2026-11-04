export const getHubRetryDelay = (attempt: number): number => {
  if (attempt < 5) {
    return 1000;
  }
  if (attempt < 15) {
    return 5000;
  }
  return 30000;
};

export const waitForHubRetry = (
  delay: number,
  signal: AbortSignal,
): Promise<void> =>
  new Promise((resolve) => {
    if (signal.aborted) {
      resolve();
      return;
    }
    const finish = () => {
      clearTimeout(timer);
      signal.removeEventListener("abort", finish);
      resolve();
    };
    const timer = setTimeout(finish, delay);
    signal.addEventListener("abort", finish, { once: true });
  });
