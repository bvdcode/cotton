import * as React from "react";
import hlsScriptUrl from "hls.js/dist/hls.min.js?url";

const HLS_PLAYLIST_MIME_TYPE = "application/vnd.apple.mpegurl";

interface HlsLevel {
  name?: string;
}

interface HlsInstance {
  levels: HlsLevel[];
  startLevel: number;
  currentLevel: number;
  loadLevel: number;
  on(event: string, callback: () => void): void;
  loadSource(src: string): void;
  startLoad(startPosition?: number): void;
  attachMedia(media: HTMLMediaElement): void;
  destroy(): void;
}

interface HlsConstructor {
  new (config: {
    autoStartLoad: boolean;
    abrEwmaDefaultEstimate: number;
    capLevelToPlayerSize: boolean;
    testBandwidth: boolean;
  }): HlsInstance;
  isSupported(): boolean;
  Events: {
    MEDIA_ATTACHED: string;
    MANIFEST_PARSED: string;
  };
}

declare global {
  interface Window {
    Hls?: HlsConstructor;
  }
}

interface HlsVideoSlideProps {
  src: string;
  poster?: string;
  width?: number;
  height?: number;
  active: boolean;
}

let hlsLoadPromise: Promise<HlsConstructor> | null = null;

function loadHls(): Promise<HlsConstructor> {
  if (window.Hls) {
    return Promise.resolve(window.Hls);
  }

  hlsLoadPromise ??= new Promise((resolve, reject) => {
    const existingScript = document.querySelector<HTMLScriptElement>(
      "script[data-cotton-hls]",
    );
    const handleLoad = () => {
      if (window.Hls) {
        resolve(window.Hls);
        return;
      }

      hlsLoadPromise = null;
      reject(new Error("hls.js loaded without exposing window.Hls"));
    };
    const handleError = () => {
      hlsLoadPromise = null;
      reject(new Error("Failed to load hls.js"));
    };

    if (existingScript) {
      existingScript.addEventListener("load", handleLoad, { once: true });
      existingScript.addEventListener("error", handleError, { once: true });
      return;
    }

    const script = document.createElement("script");
    script.src = hlsScriptUrl;
    script.async = true;
    script.dataset.cottonHls = "true";
    script.addEventListener("load", handleLoad, { once: true });
    script.addEventListener("error", handleError, { once: true });
    document.head.appendChild(script);
  });

  return hlsLoadPromise;
}

export const HlsVideoSlide: React.FC<HlsVideoSlideProps> = ({
  src,
  poster,
  width,
  height,
  active,
}) => {
  const videoRef = React.useRef<HTMLVideoElement | null>(null);

  React.useEffect(() => {
    if (!active) {
      return;
    }

    const videoElement = videoRef.current;
    if (!videoElement) {
      return;
    }

    let cancelled = false;
    let hlsInstance: HlsInstance | null = null;

    void (async () => {
      const Hls = await loadHls();
      if (cancelled || !videoRef.current) {
        return;
      }

      if (!Hls.isSupported()) {
        if (videoElement.canPlayType(HLS_PLAYLIST_MIME_TYPE)) {
          videoElement.src = src;
        }
        return;
      }

      const hls = new Hls({
        autoStartLoad: false,
        abrEwmaDefaultEstimate: 50_000_000,
        capLevelToPlayerSize: false,
        testBandwidth: false,
      });
      hlsInstance = hls;

      hls.on(Hls.Events.MEDIA_ATTACHED, () => {
        hls.loadSource(src);
      });
      hls.on(Hls.Events.MANIFEST_PARSED, () => {
        if (hls.levels.length === 0) {
          return;
        }

        const sourceByName = hls.levels.findIndex(
          (level) => level.name === "Source",
        );
        const target = sourceByName >= 0 ? sourceByName : hls.levels.length - 1;
        hls.startLevel = target;
        hls.currentLevel = target;
        hls.loadLevel = target;
        hls.startLoad(-1);
      });
      hls.attachMedia(videoRef.current);
    })();

    const mountedVideo = videoElement;
    return () => {
      cancelled = true;
      hlsInstance?.destroy();
      if (mountedVideo.src) {
        mountedVideo.removeAttribute("src");
        mountedVideo.load();
      }
    };
  }, [src, active]);

  return (
    <video
      ref={videoRef}
      poster={poster}
      controls
      playsInline
      autoPlay
      preload="metadata"
      style={{
        maxWidth: "100%",
        maxHeight: "100%",
        width: width ? `${width}px` : undefined,
        height: height ? `${height}px` : undefined,
        objectFit: "contain",
      }}
    />
  );
};
