import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
  HttpTransportType,
} from "@microsoft/signalr";
import {
  clearAccessToken,
  getAccessToken,
  refreshAccessToken,
} from "../api/httpClient";
import { getRefreshEnabled, useAuthStore } from "../store/authStore";
import { reportClientWarning } from "../utils/clientDiagnostics";
import { getHubRetryDelay, waitForHubRetry } from "./hubRetry";
import type { JsonValue } from "../types/json";
import {
  HUB_METHODS,
  SILENCED_HUB_METHODS,
  type HubMethod,
} from "./hubMethods";

type HubEventCallback = (...args: JsonValue[]) => void;
type HubConnectionCallback = () => void;

class EventHubService {
  private connection: HubConnection | null = null;
  private listeners = new Map<HubMethod, Set<HubEventCallback>>();
  private connectionListeners = new Set<HubConnectionCallback>();
  private startController: AbortController | null = null;
  private startPromise: Promise<void> | null = null;

  async start(): Promise<void> {
    if (this.startPromise) {
      return this.startPromise;
    }
    if (
      this.connection?.state === HubConnectionState.Connected ||
      this.connection?.state === HubConnectionState.Reconnecting
    ) {
      return;
    }

    const controller = new AbortController();
    this.startController = controller;
    const promise = this.connectUntilStarted(controller.signal);
    this.startPromise = promise;
    try {
      await promise;
    } finally {
      if (this.startPromise === promise) {
        this.startPromise = null;
        this.startController = null;
      }
    }
  }

  private async connectUntilStarted(signal: AbortSignal): Promise<void> {
    let attempts = 0;
    while (!signal.aborted && getRefreshEnabled()) {
      try {
        await this.initConnection(signal);
        return;
      } catch (error) {
        if (signal.aborted) {
          return;
        }
        reportClientWarning("Failed to connect to event hub", error);
      }
      await waitForHubRetry(getHubRetryDelay(attempts), signal);
      attempts += 1;
    }
  }

  private async initConnection(signal: AbortSignal): Promise<void> {
    const accessTokenFactory = async (): Promise<string> => {
      const token = getAccessToken();
      if (token) {
        return token;
      }

      if (!getRefreshEnabled()) {
        throw new Error("Hub auth refresh is disabled");
      }

      const refreshed = await refreshAccessToken();
      if (!refreshed) {
        throw new Error("Hub auth token is unavailable");
      }

      return refreshed;
    };

    const reconnectPolicy = {
      nextRetryDelayInMilliseconds: (ctx: { previousRetryCount: number }) =>
        getRefreshEnabled() ? getHubRetryDelay(ctx.previousRetryCount) : null,
    };

    const attemptSpecs: Array<
      readonly [transport: HttpTransportType, skipNegotiation?: boolean]
    > = [
      // Some servers/proxies reject /negotiate; WebSockets + skipNegotiation avoids it.
      [HttpTransportType.WebSockets, true],
      // Fallback to normal negotiation (allows LongPolling when WS isn't available).
      [HttpTransportType.WebSockets | HttpTransportType.LongPolling],
    ];

    let lastError: Error | null = null;

    for (const [transport, skipNegotiation] of attemptSpecs) {
      if (signal.aborted) {
        return;
      }
      const connection = new HubConnectionBuilder()
        .withUrl("/api/v1/hub/events", {
          accessTokenFactory,
          transport,
          skipNegotiation,
        })
        .withAutomaticReconnect(reconnectPolicy)
        .configureLogging(LogLevel.Warning)
        .build();
      this.connection = connection;
      try {
        // Registering no-op handlers prevents SignalR warnings on pages
        // that don't subscribe to file-related events.
        for (const method of SILENCED_HUB_METHODS) {
          connection.on(method, () => {
            // no-op
          });
        }

        connection.on(HUB_METHODS.SessionRevoked, () => {
          if (signal.aborted || this.connection !== connection) {
            return;
          }
          clearAccessToken();
          useAuthStore.getState().logoutLocal();
          this.dispose();
        });

        connection.onreconnected(() => {
          if (signal.aborted || this.connection !== connection) {
            return;
          }
          this.resubscribeAll();
          this.notifyConnected();
        });

        for (const [method, callbacks] of this.listeners) {
          for (const cb of callbacks) {
            connection.on(method, cb);
          }
        }

        await connection.start();
        if (signal.aborted) {
          await this.stopConnection(connection);
          return;
        }
        this.notifyConnected();
        return;
      } catch (e) {
        lastError = e instanceof Error ? e : new Error("Failed to start hub");
        await this.stopConnection(connection);
      }
    }

    throw lastError ?? new Error("Failed to start hub");
  }

  onConnected(callback: HubConnectionCallback): () => void {
    this.connectionListeners.add(callback);

    return () => {
      this.connectionListeners.delete(callback);
    };
  }

  on(method: HubMethod, callback: HubEventCallback): () => void {
    if (!this.listeners.has(method)) {
      this.listeners.set(method, new Set());
    }
    const set = this.listeners.get(method)!;
    const isFirstRegistration = !set.has(callback);
    set.add(callback);

    // SignalR allows registering handlers before and during connection start.
    // If we only register when state === Connected, we can miss attaching
    // handlers during the Connecting window and never receive events.
    if (this.connection && isFirstRegistration) {
      this.connection.on(method, callback);
    }

    return () => {
      set.delete(callback);
      if (set.size === 0) {
        this.listeners.delete(method);
      }
      this.connection?.off(method, callback);
    };
  }

  private notifyConnected(): void {
    for (const callback of this.connectionListeners) {
      callback();
    }
  }

  private resubscribeAll(): void {
    if (!this.connection) {
      return;
    }
    for (const [method, callbacks] of this.listeners) {
      for (const cb of callbacks) {
        this.connection.off(method, cb);
        this.connection.on(method, cb);
      }
    }
  }

  private async stopConnection(connection: HubConnection): Promise<void> {
    if (this.connection === connection) {
      this.connection = null;
    }
    try {
      await connection.stop();
    } catch (error) {
      reportClientWarning("Failed to stop event hub connection", error);
    }
  }

  dispose(): void {
    this.startController?.abort();
    this.startController = null;
    this.startPromise = null;
    if (this.connection) {
      void this.stopConnection(this.connection);
    }
  }
}

export const eventHub = new EventHubService();
