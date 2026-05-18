export const ROOT_RESOLVE_MIN_INTERVAL_MS = 600_000;

export const rootResolveState: {
  promise: Promise<void> | null;
  lastStartedAt: number;
} = {
  promise: null,
  lastStartedAt: 0,
};

export const resetNodesActionsInternals = (): void => {
  rootResolveState.promise = null;
  rootResolveState.lastStartedAt = 0;
};
