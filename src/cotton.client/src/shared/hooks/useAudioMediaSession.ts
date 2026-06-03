import { useEffect, useRef } from "react";
import type { AudioMediaSessionTrack } from "../types/audio";

interface UseAudioMediaSessionOptions {
  audioElement: HTMLAudioElement | null;
  track: AudioMediaSessionTrack | null;
  onPreviousTrack?: () => void;
  onNextTrack?: () => void;
}

const DEFAULT_SEEK_OFFSET_SECONDS = 10;

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

const hasMediaSession = (): boolean =>
  typeof navigator !== "undefined" && "mediaSession" in navigator;

const resolveArtworkType = (src: string): string | undefined => {
  const path = src.split(/[?#]/)[0].toLowerCase();
  if (path.endsWith(".png")) return "image/png";
  if (path.endsWith(".jpg") || path.endsWith(".jpeg")) return "image/jpeg";
  if (path.endsWith(".webp")) return "image/webp";
  if (path.endsWith(".gif")) return "image/gif";
  if (path.endsWith(".avif")) return "image/avif";
  return undefined;
};

const setActionHandler = (
  action: MediaSessionAction,
  handler: MediaSessionActionHandler | null,
): void => {
  try {
    navigator.mediaSession.setActionHandler(action, handler);
  } catch {
    // Some browsers expose Media Session but not every action.
  }
};

const clearActionHandlers = (): void => {
  for (const action of MANAGED_ACTIONS) {
    setActionHandler(action, null);
  }
};

const clampTime = (time: number, duration: number): number => {
  const nonNegativeTime = Math.max(0, time);
  return Number.isFinite(duration)
    ? Math.min(nonNegativeTime, duration)
    : nonNegativeTime;
};

const updatePositionState = (
  session: MediaSession,
  audioElement: HTMLAudioElement,
): void => {
  const duration = audioElement.duration;
  if (!Number.isFinite(duration) || duration <= 0) {
    try {
      session.setPositionState(undefined);
    } catch {
      // Browsers may reject clearing position state; stale progress is harmless.
    }
    return;
  }

  try {
    session.setPositionState({
      duration,
      position: clampTime(audioElement.currentTime, duration),
      playbackRate: audioElement.playbackRate || 1,
    });
  } catch {
    // Duration/currentTime can briefly be inconsistent while metadata settles.
  }
};

export const useAudioMediaSession = ({
  audioElement,
  track,
  onPreviousTrack,
  onNextTrack,
}: UseAudioMediaSessionOptions): void => {
  const previousTrackRef = useRef<(() => void) | undefined>(onPreviousTrack);
  const nextTrackRef = useRef<(() => void) | undefined>(onNextTrack);

  useEffect(() => {
    previousTrackRef.current = onPreviousTrack;
    nextTrackRef.current = onNextTrack;
  });

  const active = Boolean(audioElement && track);
  const title = track?.title ?? "";
  const artist = track?.artist ?? "";
  const album = track?.album ?? "";
  const artworkSrc = track?.artwork?.src;
  const artworkType = track?.artwork?.type;
  const hasPreviousTrack = Boolean(onPreviousTrack);
  const hasNextTrack = Boolean(onNextTrack);

  useEffect(() => {
    if (!hasMediaSession() || !active || typeof MediaMetadata === "undefined") {
      return;
    }

    const artwork = artworkSrc
      ? [
          {
            src: artworkSrc,
            type: artworkType ?? resolveArtworkType(artworkSrc),
          },
        ]
      : [];

    navigator.mediaSession.metadata = new MediaMetadata({
      title,
      artist,
      album,
      artwork,
    });

    return () => {
      navigator.mediaSession.metadata = null;
    };
  }, [active, title, artist, album, artworkSrc, artworkType]);

  useEffect(() => {
    if (!hasMediaSession() || !active || !audioElement) {
      return;
    }

    setActionHandler("play", () => {
      void audioElement.play().catch(() => undefined);
    });
    setActionHandler("pause", () => audioElement.pause());
    setActionHandler("stop", () => {
      audioElement.pause();
      audioElement.currentTime = 0;
    });
    setActionHandler("seekbackward", (details) => {
      const offset = details.seekOffset ?? DEFAULT_SEEK_OFFSET_SECONDS;
      audioElement.currentTime = clampTime(
        audioElement.currentTime - offset,
        audioElement.duration,
      );
    });
    setActionHandler("seekforward", (details) => {
      const offset = details.seekOffset ?? DEFAULT_SEEK_OFFSET_SECONDS;
      audioElement.currentTime = clampTime(
        audioElement.currentTime + offset,
        audioElement.duration,
      );
    });
    setActionHandler("seekto", (details) => {
      if (typeof details.seekTime !== "number") {
        return;
      }

      const nextTime = clampTime(details.seekTime, audioElement.duration);
      if (details.fastSeek && typeof audioElement.fastSeek === "function") {
        audioElement.fastSeek(nextTime);
        return;
      }
      audioElement.currentTime = nextTime;
    });
    setActionHandler(
      "previoustrack",
      hasPreviousTrack ? () => previousTrackRef.current?.() : null,
    );
    setActionHandler(
      "nexttrack",
      hasNextTrack ? () => nextTrackRef.current?.() : null,
    );

    return clearActionHandlers;
  }, [active, audioElement, hasPreviousTrack, hasNextTrack]);

  useEffect(() => {
    if (!hasMediaSession() || !active || !audioElement) {
      return;
    }

    const session = navigator.mediaSession;

    const markPlaying = (): void => {
      session.playbackState = "playing";
      updatePositionState(session, audioElement);
    };
    const markPaused = (): void => {
      session.playbackState = "paused";
      updatePositionState(session, audioElement);
    };
    const updatePosition = (): void => {
      updatePositionState(session, audioElement);
    };

    audioElement.addEventListener("play", markPlaying);
    audioElement.addEventListener("playing", markPlaying);
    audioElement.addEventListener("pause", markPaused);
    audioElement.addEventListener("ended", markPaused);
    audioElement.addEventListener("loadedmetadata", updatePosition);
    audioElement.addEventListener("durationchange", updatePosition);
    audioElement.addEventListener("timeupdate", updatePosition);
    audioElement.addEventListener("seeked", updatePosition);
    audioElement.addEventListener("ratechange", updatePosition);

    if (audioElement.paused) {
      markPaused();
    } else {
      markPlaying();
    }

    return () => {
      audioElement.removeEventListener("play", markPlaying);
      audioElement.removeEventListener("playing", markPlaying);
      audioElement.removeEventListener("pause", markPaused);
      audioElement.removeEventListener("ended", markPaused);
      audioElement.removeEventListener("loadedmetadata", updatePosition);
      audioElement.removeEventListener("durationchange", updatePosition);
      audioElement.removeEventListener("timeupdate", updatePosition);
      audioElement.removeEventListener("seeked", updatePosition);
      audioElement.removeEventListener("ratechange", updatePosition);
      session.playbackState = "none";
    };
  }, [active, audioElement]);
};
