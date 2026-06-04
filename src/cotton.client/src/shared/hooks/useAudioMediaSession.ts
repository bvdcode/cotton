import type { MediaSessionTrackInfo } from "../types/mediaSession";
import { useMediaSessionSource } from "./useMediaSessionSource";
import { MEDIA_SESSION_SOURCE_PRIORITY } from "../utils/mediaSessionCoordinator";

interface UseAudioMediaSessionOptions {
  audioElement: HTMLAudioElement | null;
  track: MediaSessionTrackInfo | null;
  onPreviousTrack?: () => void;
  onNextTrack?: () => void;
}

export const useAudioMediaSession = ({
  audioElement,
  track,
  onPreviousTrack,
  onNextTrack,
}: UseAudioMediaSessionOptions): void => {
  useMediaSessionSource({
    mediaElement: audioElement,
    track,
    priority: MEDIA_SESSION_SOURCE_PRIORITY.audio,
    onPreviousTrack,
    onNextTrack,
  });
};
