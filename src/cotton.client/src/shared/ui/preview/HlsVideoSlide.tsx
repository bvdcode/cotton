import * as React from "react";
import hlsScriptUrl from "hls.js/dist/hls.min.js?url";

const HLS_PLAYLIST_MIME_TYPE = "application/vnd.apple.mpegurl";
const TRANSCODE_NOTICE_MS = 7000;

interface HlsInstance {
  on(event: string, callback: () => void): void;
  loadSource(src: string): void;
  startLoad(startPosition?: number): void;
  attachMedia(media: HTMLMediaElement): void;
  destroy(): void;
}

interface HlsConstructor {
  new (config: {
    autoStartLoad?: boolean;
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
  noticeText: string;
  errorText: string;
}

type HlsSlideStatusState = {
  key: string;
  noticeVisible: boolean;
  loadFailed: boolean;
};

const createHlsSlideStatusState = (
  key: string,
  active: boolean,
): HlsSlideStatusState => ({
  key,
  noticeVisible: active,
  loadFailed: false,
});

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
  noticeText,
  errorText,
}) => {
  const videoRef = React.useRef<HTMLVideoElement | null>(null);
  const statusKey = active ? src : "";
  const [statusState, setStatusState] = React.useState<HlsSlideStatusState>(
    () => createHlsSlideStatusState(statusKey, active),
  );
  const status =
    statusState.key === statusKey
      ? statusState
      : createHlsSlideStatusState(statusKey, active);
  const { noticeVisible, loadFailed } = status;

  React.useEffect(() => {
    if (!active) {
      return;
    }

    const timer = window.setTimeout(() => {
      setStatusState((current) => {
        const base =
          current.key === statusKey
            ? current
            : createHlsSlideStatusState(statusKey, active);
        return { ...base, noticeVisible: false };
      });
    }, TRANSCODE_NOTICE_MS);

    return () => {
      window.clearTimeout(timer);
    };
  }, [active, statusKey]);

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
      let Hls: HlsConstructor;
      try {
        Hls = await loadHls();
      } catch {
        if (!cancelled && videoElement.canPlayType(HLS_PLAYLIST_MIME_TYPE)) {
          videoElement.src = src;
          return;
        }

        if (!cancelled) {
          setStatusState((current) => {
          const base =
            current.key === statusKey
              ? current
              : createHlsSlideStatusState(statusKey, active);
          return { ...base, loadFailed: true, noticeVisible: false };
        });
        }
        return;
      }

      if (cancelled || !videoRef.current) {
        return;
      }

      if (!Hls.isSupported()) {
        if (videoElement.canPlayType(HLS_PLAYLIST_MIME_TYPE)) {
          videoElement.src = src;
          return;
        }
        setStatusState((current) => {
          const base =
            current.key === statusKey
              ? current
              : createHlsSlideStatusState(statusKey, active);
          return { ...base, loadFailed: true, noticeVisible: false };
        });
        return;
      }

      const hls = new Hls({
        autoStartLoad: false,
      });
      hlsInstance = hls;

      hls.on(Hls.Events.MEDIA_ATTACHED, () => {
        hls.loadSource(src);
      });
      hls.on(Hls.Events.MANIFEST_PARSED, () => {
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
  }, [active, src, statusKey]);

  return (
    <div className="media-lightbox__hls-slide">
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
      {noticeVisible && !loadFailed && (
        <div className="media-lightbox__hls-notice" role="status">
          {noticeText}
        </div>
      )}
      {loadFailed && (
        <div className="media-lightbox__hls-error" role="alert">
          {errorText}
        </div>
      )}
    </div>
  );
};
