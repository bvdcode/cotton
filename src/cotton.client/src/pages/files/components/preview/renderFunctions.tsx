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
    const width = attrs.style?.width
      ? parseFloat(attrs.style.width as string)
      : VIDEO_WIDTH;
    const offset = (width - VIDEO_WIDTH) / VIDEO_WIDTH;
    const childScale = scale === 1 ? scale + offset : 1 + offset;

    return (
      <div {...attrs}>
        <div
          style={{
            width: VIDEO_WIDTH,
            height: VIDEO_HEIGHT,
            transformOrigin: "0 0",
            transform: `scale(${childScale})`,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <LazyVideoContent
            fileId={fileId}
            fileName={fileName}
            width={VIDEO_WIDTH}
            height={VIDEO_HEIGHT}
          />
        </div>
      </div>
    );
  };
};
