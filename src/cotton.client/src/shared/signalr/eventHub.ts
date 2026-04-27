import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
  HttpTransportType,
} from "@microsoft/signalr";
import { getAccessToken, refreshAccessToken } from "../api/httpClient";
import { getRefreshEnabled } from "../store/authStore";
import type { JsonValue } from "../types/json";

type HubEventCallback = (...args: JsonValue[]) => void;

const SILENCED_METHODS: ReadonlyArray<string> = [
  "FileCreated",
  "FileUpdated",
  "FileDeleted",
  "FileMoved",
  "FileRenamed",
  "NodeCreated",
  "NodeDeleted",
  "NodeMoved",
  "NodeRenamed",
  "PreviewGenerated",
].flatMap((m) => [m, m.toLowerCase()]);

class EventHubService {
  private connection: HubConnection | null = null;
  private listeners = new Map<string, Set<HubEventCallback>>();
  private started = false;
  private startPromise: Promise<void> | null = null;

  async start(): Promise<void> {
    if (
      this.started &&
      this.connection?.state === HubConnectionState.Connected
    ) {
      return;
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    this.startPromise = this.initConnection();
    try {
      await this.startPromise;
    } finally {
      this.startPromise = null;
    }
  }

  private async initConnection(): Promise<void> {
    this.dispose();

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
      nextRetryDelayInMilliseconds: (ctx: { previousRetryCount: number }) => {
        if (ctx.previousRetryCount < 5) return 1000;
        if (ctx.previousRetryCount < 15) return 5000;
        return 30000;
      },
    };

    const attemptSpecs: Array<{
      transport: HttpTransportType;
      skipNegotiation?: boolean;
    }> = [
      // Some servers/proxies reject /negotiate; WebSockets + skipNegotiation avoids it.
      { transport: HttpTransportType.WebSockets, skipNegotiation: true },
      // Fallback to normal negotiation (allows LongPolling when WS isn't available).
      {
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      },
    ];

    let lastError: Error | null = null;

    for (const spec of attemptSpecs) {
      try {
        this.connection = new HubConnectionBuilder()
          .withUrl("/api/v1/hub/events", {
            accessTokenFactory,
            transport: spec.transport,
            skipNegotiation: spec.skipNegotiation,
          })
          .withAutomaticReconnect(reconnectPolicy)
          .configureLogging(LogLevel.Warning)
          .build();

        // Some server versions emit lowercased method names.
        // Registering no-op handlers prevents SignalR warnings on pages
        // that don't subscribe to file-related events.
        for (const method of SILENCED_METHODS) {
          this.connection.on(method, () => {
            // no-op
          });
        }

        this.connection.onreconnected(() => {
          this.resubscribeAll();
        });

        for (const [method, callbacks] of this.listeners) {
          for (const cb of callbacks) {
            this.connection.on(method, cb);
          }
        }

        await this.connection.start();
        this.started = true;
        return;
      } catch (e) {
        lastError = e instanceof Error ? e : new Error("Failed to start hub");
        this.dispose();
      }
    }

    throw lastError ?? new Error("Failed to start hub");
  }

  on(method: string, callback: HubEventCallback): () => void {
    if (!this.listeners.has(method)) {
      this.listeners.set(method, new Set());
    }
    const set = this.listeners.get(method)!;
    const wrapped = callback;
    set.add(wrapped);

    // SignalR allows registering handlers before and during connection start.
    // If we only register when state === Connected, we can miss attaching
    // handlers during the Connecting window and never receive events.
    if (this.connection) {
      this.connection.on(method, wrapped);
    }

    return () => {
      set.delete(wrapped);
      if (set.size === 0) {
        this.listeners.delete(method);
      }
      this.connection?.off(method, wrapped);
    };
  }

  private resubscribeAll(): void {
    if (!this.connection) return;
    for (const [method, callbacks] of this.listeners) {
      for (const cb of callbacks) {
        this.connection.off(method, cb);
        this.connection.on(method, cb);
      }
    }
  }

  dispose(): void {
    if (this.connection) {
      this.connection.stop();
      this.connection = null;
    }
    this.started = false;
    this.startPromise = null;
  }
}

export const eventHub = new EventHubService();
