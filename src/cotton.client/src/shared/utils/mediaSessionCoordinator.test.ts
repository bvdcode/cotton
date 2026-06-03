import { describe, expect, it, vi } from "vitest";
import type { MediaSessionTrackInfo } from "../types/mediaSession";
import {
  MEDIA_SESSION_SOURCE_PRIORITY,
  MediaSessionCoordinator,
  type MediaSessionPlatform,
} from "./mediaSessionCoordinator";

class FakeMediaSessionPlatform implements MediaSessionPlatform {
  metadata: MediaSessionTrackInfo | null = null;
  playbackState: MediaSessionPlaybackState = "none";
  positionState: MediaPositionState | null = null;
  actions = new Map<MediaSessionAction, MediaSessionActionHandler | null>();

  isSupported(): boolean {
    return true;
  }

  setMetadata(track: MediaSessionTrackInfo | null): void {
    this.metadata = track;
  }

  setPlaybackState(state: MediaSessionPlaybackState): void {
    this.playbackState = state;
  }

  setPositionState(state: MediaPositionState | null): void {
    this.positionState = state;
  }

  setActionHandler(
    action: MediaSessionAction,
    handler: MediaSessionActionHandler | null,
  ): void {
    this.actions.set(action, handler);
  }
}

const createMediaElement = ({
  paused,
  duration = 120,
  currentTime = 0,
}: {
  paused: boolean;
  duration?: number;
  currentTime?: number;
}): HTMLMediaElement => {
  let pausedState = paused;

  const mediaElement = {
    get paused() {
      return pausedState;
    },
    get duration() {
      return duration;
    },
    currentTime,
    playbackRate: 1,
    play: vi.fn(async () => {
      pausedState = false;
    }),
    pause: vi.fn(() => {
      pausedState = true;
    }),
    fastSeek: vi.fn((time: number) => {
      mediaElement.currentTime = time;
    }),
  } as unknown as HTMLMediaElement;

  return mediaElement;
};

describe("MediaSessionCoordinator", () => {
  it("keeps playing audio as owner while a higher-priority video is only paused", () => {
    const platform = new FakeMediaSessionPlatform();
    const coordinator = new MediaSessionCoordinator(platform);
    const audio = createMediaElement({ paused: false });
    const video = createMediaElement({ paused: true });

    coordinator.upsertSource({
      id: "audio",
      priority: MEDIA_SESSION_SOURCE_PRIORITY.audio,
      mediaElement: audio,
      track: { title: "Song" },
    });
    coordinator.upsertSource({
      id: "video",
      priority: MEDIA_SESSION_SOURCE_PRIORITY.video,
      mediaElement: video,
      track: { title: "Clip" },
    });

    expect(coordinator.getOwnerId()).toBe("audio");
    expect(platform.metadata?.title).toBe("Song");
    expect(platform.playbackState).toBe("playing");
  });

  it("lets playing video take over and returns to playing audio when video pauses", () => {
    const platform = new FakeMediaSessionPlatform();
    const coordinator = new MediaSessionCoordinator(platform);
    const audio = createMediaElement({ paused: false });
    const video = createMediaElement({ paused: true });

    coordinator.upsertSource({
      id: "audio",
      priority: MEDIA_SESSION_SOURCE_PRIORITY.audio,
      mediaElement: audio,
      track: { title: "Song" },
    });
    coordinator.upsertSource({
      id: "video",
      priority: MEDIA_SESSION_SOURCE_PRIORITY.video,
      mediaElement: video,
      track: { title: "Clip" },
    });

    coordinator.updateSourcePlayback("video", "playing");
    expect(coordinator.getOwnerId()).toBe("video");
    expect(platform.metadata?.title).toBe("Clip");

    coordinator.updateSourcePlayback("video", "paused");
    expect(coordinator.getOwnerId()).toBe("audio");
    expect(platform.metadata?.title).toBe("Song");
  });

  it("routes media actions to the current owner", () => {
    const platform = new FakeMediaSessionPlatform();
    const coordinator = new MediaSessionCoordinator(platform);
    const video = createMediaElement({
      paused: false,
      duration: 90,
      currentTime: 20,
    });
    const onNextTrack = vi.fn();

    coordinator.upsertSource({
      id: "video",
      priority: MEDIA_SESSION_SOURCE_PRIORITY.video,
      mediaElement: video,
      track: { title: "Clip" },
      onNextTrack,
    });

    platform.actions.get("seekforward")?.({
      action: "seekforward",
      seekOffset: 15,
    });
    platform.actions.get("nexttrack")?.({ action: "nexttrack" });

    expect(video.currentTime).toBe(35);
    expect(onNextTrack).toHaveBeenCalledOnce();
  });

  it("clears platform state when the last source is removed", () => {
    const platform = new FakeMediaSessionPlatform();
    const coordinator = new MediaSessionCoordinator(platform);

    coordinator.upsertSource({
      id: "audio",
      priority: MEDIA_SESSION_SOURCE_PRIORITY.audio,
      mediaElement: createMediaElement({ paused: false }),
      track: { title: "Song" },
    });
    coordinator.removeSource("audio");

    expect(coordinator.getOwnerId()).toBeNull();
    expect(platform.metadata).toBeNull();
    expect(platform.playbackState).toBe("none");
    expect([...platform.actions.values()].every((handler) => handler === null))
      .toBe(true);
  });
});
