import { useEffect, useState } from "react";
import type { MediaSessionTrackInfo } from "../types/mediaSession";
import { mediaSessionCoordinator } from "../utils/mediaSessionCoordinator";

interface UseMediaSessionSourceOptions {
  mediaElement: HTMLMediaElement | null;
  track: MediaSessionTrackInfo | null;
  priority: number;
  enabled?: boolean;
  onPreviousTrack?: () => void;
  onNextTrack?: () => void;
}

let nextSourceId = 0;

const createSourceId = (): string => {
  nextSourceId += 1;
  return `media-session-source-${nextSourceId}`;
};

export const useMediaSessionSource = ({
  mediaElement,
  track,
  priority,
  enabled = true,
  onPreviousTrack,
  onNextTrack,
}: UseMediaSessionSourceOptions): void => {
  const [sourceId] = useState(createSourceId);
  const active = Boolean(enabled && mediaElement && track);

  useEffect(() => {
    if (!active || !mediaElement || !track) {
      mediaSessionCoordinator.removeSource(sourceId);
      return;
    }

    mediaSessionCoordinator.upsertSource({
      id: sourceId,
      priority,
      mediaElement,
      track,
      onPreviousTrack,
      onNextTrack,
    });
  }, [
    active,
    mediaElement,
    onNextTrack,
    onPreviousTrack,
    priority,
    sourceId,
    track,
  ]);

  useEffect(() => {
    return () => {
      mediaSessionCoordinator.removeSource(sourceId);
    };
  }, [sourceId]);

  useEffect(() => {
    if (!active || !mediaElement) {
      return;
    }

    const markPlaying = (): void => {
      mediaSessionCoordinator.updateSourcePlayback(sourceId, "playing");
    };
    const markPaused = (): void => {
      mediaSessionCoordinator.updateSourcePlayback(sourceId, "paused");
    };
    const updatePosition = (): void => {
      mediaSessionCoordinator.updateSourcePosition(sourceId);
    };

    mediaElement.addEventListener("play", markPlaying);
    mediaElement.addEventListener("playing", markPlaying);
    mediaElement.addEventListener("pause", markPaused);
    mediaElement.addEventListener("ended", markPaused);
    mediaElement.addEventListener("loadedmetadata", updatePosition);
    mediaElement.addEventListener("durationchange", updatePosition);
    mediaElement.addEventListener("timeupdate", updatePosition);
    mediaElement.addEventListener("seeked", updatePosition);
    mediaElement.addEventListener("ratechange", updatePosition);

    if (mediaElement.paused) {
      markPaused();
    } else {
      markPlaying();
    }

    return () => {
      mediaElement.removeEventListener("play", markPlaying);
      mediaElement.removeEventListener("playing", markPlaying);
      mediaElement.removeEventListener("pause", markPaused);
      mediaElement.removeEventListener("ended", markPaused);
      mediaElement.removeEventListener("loadedmetadata", updatePosition);
      mediaElement.removeEventListener("durationchange", updatePosition);
      mediaElement.removeEventListener("timeupdate", updatePosition);
      mediaElement.removeEventListener("seeked", updatePosition);
      mediaElement.removeEventListener("ratechange", updatePosition);
    };
  }, [active, mediaElement, sourceId]);
};
