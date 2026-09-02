import { useCallback, useEffect, useRef } from "react";
import type { JsonValue } from "../types/json";
import { eventHub } from "./eventHub";
import {
  FILE_AND_NODE_MUTATION_HUB_METHODS,
  getHubMethodVariants,
  type HubMethodOrLower,
} from "./hubMethods";

const FILE_TREE_MUTATION_METHODS = getHubMethodVariants(
  FILE_AND_NODE_MUTATION_HUB_METHODS,
);
const INVALIDATION_DELAY_MS = 250;
const INVALIDATION_MAX_WAIT_MS = 1000;

interface UseFileTreeRealtimeInvalidationOptions {
  enabled: boolean;
  onInvalidate: () => void;
  shouldInvalidate?: (method: HubMethodOrLower, args: JsonValue[]) => boolean;
}

export const useFileTreeRealtimeInvalidation = ({
  enabled,
  onInvalidate,
  shouldInvalidate,
}: UseFileTreeRealtimeInvalidationOptions): (() => void) => {
  const onInvalidateRef = useRef(onInvalidate);
  const shouldInvalidateRef = useRef(shouldInvalidate);
  const delayTimerRef = useRef<number | null>(null);
  const maxWaitTimerRef = useRef<number | null>(null);

  useEffect(() => {
    onInvalidateRef.current = onInvalidate;
  }, [onInvalidate]);

  useEffect(() => {
    shouldInvalidateRef.current = shouldInvalidate;
  }, [shouldInvalidate]);

  const clearScheduledInvalidation = useCallback((): void => {
    if (delayTimerRef.current !== null) {
      window.clearTimeout(delayTimerRef.current);
      delayTimerRef.current = null;
    }

    if (maxWaitTimerRef.current !== null) {
      window.clearTimeout(maxWaitTimerRef.current);
      maxWaitTimerRef.current = null;
    }
  }, []);

  const flushInvalidation = useCallback((): void => {
    clearScheduledInvalidation();
    onInvalidateRef.current();
  }, [clearScheduledInvalidation]);

  const scheduleInvalidate = useCallback((): void => {
    if (delayTimerRef.current !== null) {
      window.clearTimeout(delayTimerRef.current);
    }

    delayTimerRef.current = window.setTimeout(
      flushInvalidation,
      INVALIDATION_DELAY_MS,
    );
    maxWaitTimerRef.current ??= window.setTimeout(
      flushInvalidation,
      INVALIDATION_MAX_WAIT_MS,
    );
  }, [flushInvalidation]);

  useEffect(() => clearScheduledInvalidation, [clearScheduledInvalidation]);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    eventHub.start().catch(() => {
      // Connection retries are managed by SignalR.
    });

    const unsubscribes = FILE_TREE_MUTATION_METHODS.map((method) =>
      eventHub.on(method, (...args: JsonValue[]) => {
        const predicate = shouldInvalidateRef.current;
        if (predicate && !predicate(method, args)) {
          return;
        }

        scheduleInvalidate();
      }),
    );

    return () => {
      clearScheduledInvalidation();
      for (const unsubscribe of unsubscribes) {
        unsubscribe();
      }
    };
  }, [clearScheduledInvalidation, enabled, scheduleInvalidate]);

  return scheduleInvalidate;
};
