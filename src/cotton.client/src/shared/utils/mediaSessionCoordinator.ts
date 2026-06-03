import type { MediaSessionTrackInfo } from "../types/mediaSession";

export const MEDIA_SESSION_SOURCE_PRIORITY = {
  audio: 10,
  video: 20,
} as const;

type SourcePlaybackState = "playing" | "paused";

interface MediaSessionSource {
  id: string;
  priority: number;
  mediaElement: HTMLMediaElement;
  track: MediaSessionTrackInfo;
  playbackState: SourcePlaybackState;
  lastActivity: number;
  onPreviousTrack?: () => void;
  onNextTrack?: () => void;
}

export interface MediaSessionSourceInput {
  id: string;
  priority: number;
  mediaElement: HTMLMediaElement;
  track: MediaSessionTrackInfo;
  onPreviousTrack?: () => void;
  onNextTrack?: () => void;
}

export interface MediaSessionPlatform {
  isSupported(): boolean;
  setMetadata(track: MediaSessionTrackInfo | null): void;
  setPlaybackState(state: MediaSessionPlaybackState): void;
  setPositionState(state: MediaPositionState | null): void;
  setActionHandler(
    action: MediaSessionAction,
    handler: MediaSessionActionHandler | null,
  ): void;
}

const MANAGED_ACTIONS: readonly MediaSessionAction[] = [
  "play",
  "pause",
  "stop",
  "seekbackward",
  "seekforward",
  "seekto",
  "previoustrack",
  "nexttrack",
];

const DEFAULT_SEEK_OFFSET_SECONDS = 10;

const resolveArtworkType = (src: string): string | undefined => {
  const path = src.split(/[?#]/)[0].toLowerCase();
  if (path.endsWith(".png")) return "image/png";
  if (path.endsWith(".jpg") || path.endsWith(".jpeg")) return "image/jpeg";
  if (path.endsWith(".webp")) return "image/webp";
  if (path.endsWith(".gif")) return "image/gif";
  if (path.endsWith(".avif")) return "image/avif";
  return undefined;
};

const clampTime = (time: number, duration: number): number => {
  const nonNegativeTime = Math.max(0, time);
  return Number.isFinite(duration)
    ? Math.min(nonNegativeTime, duration)
    : nonNegativeTime;
};

const readPlaybackState = (
  mediaElement: HTMLMediaElement,
): SourcePlaybackState => (mediaElement.paused ? "paused" : "playing");

const buildPositionState = (
  mediaElement: HTMLMediaElement,
): MediaPositionState | null => {
  const duration = mediaElement.duration;
  if (!Number.isFinite(duration) || duration <= 0) {
    return null;
  }

  return {
    duration,
    position: clampTime(mediaElement.currentTime, duration),
    playbackRate: mediaElement.playbackRate || 1,
  };
};

const buildMetadataKey = (track: MediaSessionTrackInfo | null): string => {
  if (!track) {
    return "";
  }

  return JSON.stringify({
    title: track.title,
    artist: track.artist ?? "",
    album: track.album ?? "",
    artworkSrc: track.artwork?.src ?? "",
    artworkType: track.artwork?.type ?? "",
  });
};

const browserMediaSessionPlatform: MediaSessionPlatform = {
  isSupported: () =>
    typeof navigator !== "undefined" && "mediaSession" in navigator,

  setMetadata: (track) => {
    if (
      typeof navigator === "undefined" ||
      !("mediaSession" in navigator)
    ) {
      return;
    }

    if (!track) {
      navigator.mediaSession.metadata = null;
      return;
    }

    if (typeof MediaMetadata === "undefined") {
      return;
    }

    const artwork = track.artwork
      ? [
          {
            src: track.artwork.src,
            type: track.artwork.type ?? resolveArtworkType(track.artwork.src),
          },
        ]
      : [];

    navigator.mediaSession.metadata = new MediaMetadata({
      title: track.title,
      artist: track.artist ?? "",
      album: track.album ?? "",
      artwork,
    });
  },

  setPlaybackState: (state) => {
    if (
      typeof navigator === "undefined" ||
      !("mediaSession" in navigator)
    ) {
      return;
    }

    navigator.mediaSession.playbackState = state;
  },

  setPositionState: (state) => {
    if (
      typeof navigator === "undefined" ||
      !("mediaSession" in navigator) ||
      typeof navigator.mediaSession.setPositionState !== "function"
    ) {
      return;
    }

    try {
      if (state) {
        navigator.mediaSession.setPositionState(state);
      } else {
        navigator.mediaSession.setPositionState();
      }
    } catch {
      // Browsers can reject transient currentTime/duration combinations.
    }
  },

  setActionHandler: (action, handler) => {
    if (
      typeof navigator === "undefined" ||
      !("mediaSession" in navigator)
    ) {
      return;
    }

    try {
      navigator.mediaSession.setActionHandler(action, handler);
    } catch {
      // Some browsers expose Media Session but not every action.
    }
  },
};

export class MediaSessionCoordinator {
  private readonly platform: MediaSessionPlatform;
  private readonly sources = new Map<string, MediaSessionSource>();
  private ownerId: string | null = null;
  private metadataKey = "";
  private activityCounter = 0;

  constructor(platform: MediaSessionPlatform = browserMediaSessionPlatform) {
    this.platform = platform;
  }

  upsertSource(input: MediaSessionSourceInput): void {
    const previous = this.sources.get(input.id);
    const sameElement = previous?.mediaElement === input.mediaElement;
    const playbackState = sameElement
      ? previous.playbackState
      : readPlaybackState(input.mediaElement);

    this.sources.set(input.id, {
      ...input,
      playbackState,
      lastActivity: sameElement
        ? previous.lastActivity
        : this.nextActivity(),
    });
    this.refresh();
  }

  removeSource(id: string): void {
    if (!this.sources.delete(id)) {
      return;
    }
    this.refresh();
  }

  updateSourcePlayback(id: string, playbackState: SourcePlaybackState): void {
    const source = this.sources.get(id);
    if (!source || source.playbackState === playbackState) {
      return;
    }

    source.playbackState = playbackState;
    source.lastActivity = this.nextActivity();
    this.refresh();
  }

  updateSourcePosition(id: string): void {
    const owner = this.getOwner();
    if (owner?.id !== id || !this.platform.isSupported()) {
      return;
    }

    this.platform.setPositionState(buildPositionState(owner.mediaElement));
  }

  getOwnerId(): string | null {
    return this.ownerId;
  }

  private nextActivity(): number {
    this.activityCounter += 1;
    return this.activityCounter;
  }

  private getOwner(): MediaSessionSource | null {
    return this.ownerId ? this.sources.get(this.ownerId) ?? null : null;
  }

  private chooseOwner(): MediaSessionSource | null {
    const candidates = [...this.sources.values()];
    const playing = candidates.filter(
      (source) => source.playbackState === "playing",
    );
    const pool = playing.length > 0 ? playing : candidates;

    pool.sort((left, right) => {
      if (left.priority !== right.priority) {
        return right.priority - left.priority;
      }
      return right.lastActivity - left.lastActivity;
    });

    return pool[0] ?? null;
  }

  private refresh(): void {
    if (!this.platform.isSupported()) {
      return;
    }

    const owner = this.chooseOwner();
    const nextOwnerId = owner?.id ?? null;
    const nextMetadataKey = buildMetadataKey(owner?.track ?? null);
    const ownerChanged = this.ownerId !== nextOwnerId;
    const metadataChanged = this.metadataKey !== nextMetadataKey;

    this.ownerId = nextOwnerId;
    this.metadataKey = nextMetadataKey;

    if (!owner) {
      this.platform.setMetadata(null);
      this.platform.setPlaybackState("none");
      this.platform.setPositionState(null);
      this.clearActionHandlers();
      return;
    }

    if (ownerChanged || metadataChanged) {
      this.platform.setMetadata(owner.track);
    }

    this.platform.setPlaybackState(owner.playbackState);
    this.platform.setPositionState(buildPositionState(owner.mediaElement));
    this.applyActionHandlers(owner);
  }

  private applyActionHandlers(owner: MediaSessionSource): void {
    this.platform.setActionHandler("play", () => {
      void owner.mediaElement.play().catch(() => undefined);
    });
    this.platform.setActionHandler("pause", () => owner.mediaElement.pause());
    this.platform.setActionHandler("stop", () => {
      owner.mediaElement.pause();
      owner.mediaElement.currentTime = 0;
    });
    this.platform.setActionHandler("seekbackward", (details) => {
      const offset = details.seekOffset ?? DEFAULT_SEEK_OFFSET_SECONDS;
      owner.mediaElement.currentTime = clampTime(
        owner.mediaElement.currentTime - offset,
        owner.mediaElement.duration,
      );
    });
    this.platform.setActionHandler("seekforward", (details) => {
      const offset = details.seekOffset ?? DEFAULT_SEEK_OFFSET_SECONDS;
      owner.mediaElement.currentTime = clampTime(
        owner.mediaElement.currentTime + offset,
        owner.mediaElement.duration,
      );
    });
    this.platform.setActionHandler("seekto", (details) => {
      if (typeof details.seekTime !== "number") {
        return;
      }

      const nextTime = clampTime(
        details.seekTime,
        owner.mediaElement.duration,
      );
      if (
        details.fastSeek &&
        typeof owner.mediaElement.fastSeek === "function"
      ) {
        owner.mediaElement.fastSeek(nextTime);
        return;
      }
      owner.mediaElement.currentTime = nextTime;
    });
    this.platform.setActionHandler(
      "previoustrack",
      owner.onPreviousTrack ? () => owner.onPreviousTrack?.() : null,
    );
    this.platform.setActionHandler(
      "nexttrack",
      owner.onNextTrack ? () => owner.onNextTrack?.() : null,
    );
  }

  private clearActionHandlers(): void {
    for (const action of MANAGED_ACTIONS) {
      this.platform.setActionHandler(action, null);
    }
  }
}

export const mediaSessionCoordinator = new MediaSessionCoordinator();
