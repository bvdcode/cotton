import { useCallback, useEffect, useRef, useState } from "react";
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
const TRACK_CHANGE_PAUSE_GRACE_MS = 1500;
const HAVE_CURRENT_DATA = 2;
const NETWORK_LOADING = 2;

const createSourceId = (): string => {
  nextSourceId += 1;
  return `media-session-source-${nextSourceId}`;
};

const isReloadingMedia = (mediaElement: HTMLMediaElement): boolean =>
  mediaElement.readyState < HAVE_CURRENT_DATA ||
  mediaElement.networkState === NETWORK_LOADING;

export const useMediaSessionSource = ({
  mediaElement,
  track,
  priority,
  enabled = true,
  onPreviousTrack,
  onNextTrack,
}: UseMediaSessionSourceOptions): void => {
  const [sourceId] = useState(createSourceId);
  const previousTrackRef = useRef<MediaSessionTrackInfo | null>(null);
  const pauseGraceUntilRef = useRef(0);
  const pauseGraceTimerRef = useRef<number | null>(null);
  const active = Boolean(enabled && mediaElement && track);

  const clearPauseGraceTimer = useCallback((): void => {
    if (pauseGraceTimerRef.current === null) {
      return;
    }

    window.clearTimeout(pauseGraceTimerRef.current);
    pauseGraceTimerRef.current = null;
  }, []);

  useEffect(() => {
    if (!active || !mediaElement || !track) {
      mediaSessionCoordinator.removeSource(sourceId);
      previousTrackRef.current = null;
      pauseGraceUntilRef.current = 0;
      clearPauseGraceTimer();
      return;
    }

    const previousTrack = previousTrackRef.current;
    if (previousTrack && previousTrack !== track && !mediaElement.paused) {
      const graceUntil = Date.now() + TRACK_CHANGE_PAUSE_GRACE_MS;
      pauseGraceUntilRef.current = graceUntil;
      clearPauseGraceTimer();
      pauseGraceTimerRef.current = window.setTimeout(() => {
        if (pauseGraceUntilRef.current !== graceUntil) {
          return;
        }

        pauseGraceUntilRef.current = 0;
        pauseGraceTimerRef.current = null;
        if (mediaElement.paused) {
          mediaSessionCoordinator.updateSourcePlayback(sourceId, "paused");
        }
      }, TRACK_CHANGE_PAUSE_GRACE_MS);
    }

    previousTrackRef.current = track;
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
    clearPauseGraceTimer,
  ]);

  useEffect(() => {
    return () => {
      clearPauseGraceTimer();
      mediaSessionCoordinator.removeSource(sourceId);
    };
  }, [sourceId, clearPauseGraceTimer]);

  useEffect(() => {
    if (!active || !mediaElement) {
      return;
    }

    const markPlaying = (): void => {
      pauseGraceUntilRef.current = 0;
      clearPauseGraceTimer();
      mediaSessionCoordinator.updateSourcePlayback(sourceId, "playing");
    };
    const markPaused = (): void => {
      if (
        pauseGraceUntilRef.current > Date.now() &&
        isReloadingMedia(mediaElement)
      ) {
        mediaSessionCoordinator.updateSourcePlayback(sourceId, "playing");
        return;
      }

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
  }, [active, mediaElement, sourceId, clearPauseGraceTimer]);
};
