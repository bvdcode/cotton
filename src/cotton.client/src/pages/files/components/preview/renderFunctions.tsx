import { LazyImageContent } from "./LazyImage";
import { LazyVideoContent, VIDEO_WIDTH, VIDEO_HEIGHT } from "./VideoPreview";

interface PhotoRenderParams {
  attrs: React.HTMLAttributes<HTMLDivElement> & { style?: React.CSSProperties };
  scale: number;
}

// Render function for PhotoView - images
export const renderLazyImage = (fileId: string, fileName: string) => {
  return ({ attrs }: PhotoRenderParams) => {
    const { style, ...rest } = attrs;
    return (
      <div
        {...rest}
        style={{
          ...style,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <LazyImageContent fileId={fileId} fileName={fileName} />
      </div>
    );
  };
};

// Render function for PhotoView - videos with lazy loading
export const renderVideoPreview = (fileId: string, fileName: string) => {
  return ({ attrs, scale }: PhotoRenderParams) => {
    const { style, ...rest } = attrs;

    // Use viewport-aware sizing for mobile
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const isMobile = viewportWidth < 768;

    let videoWidth: number;
    let videoHeight: number;

    if (isMobile) {
      // On mobile: fit video to viewport width with padding
      const maxWidth = viewportWidth - 32; // 16px padding on each side
      const maxHeight = viewportHeight * 0.7;
      const aspectRatio = VIDEO_WIDTH / VIDEO_HEIGHT;

      if (maxWidth / aspectRatio <= maxHeight) {
        videoWidth = maxWidth;
        videoHeight = maxWidth / aspectRatio;
      } else {
        videoHeight = maxHeight;
        videoWidth = maxHeight * aspectRatio;
      }
    } else {
      videoWidth = VIDEO_WIDTH;
      videoHeight = VIDEO_HEIGHT;
    }

    const baseWidth = attrs.style?.width
      ? parseFloat(attrs.style.width as string)
      : videoWidth;
    const offset = (baseWidth - videoWidth) / videoWidth;
    const childScale = scale === 1 ? scale + offset : 1 + offset;

    return (
      <div
        {...rest}
        style={{
          ...style,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <div
          style={{
            width: videoWidth,
            height: videoHeight,
            transformOrigin: "center center",
            transform: `scale(${childScale})`,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <LazyVideoContent
            fileId={fileId}
            fileName={fileName}
            width={videoWidth}
            height={videoHeight}
          />
        </div>
      </div>
    );
  };
};
