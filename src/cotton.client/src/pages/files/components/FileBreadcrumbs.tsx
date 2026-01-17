import { useMemo, useRef, useEffect, useLayoutEffect } from "react";
import { Breadcrumbs, Link as MuiLink, Typography, Box } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { useTranslation } from "react-i18next";

interface Breadcrumb {
  id: string;
  name: string;
}

interface FileBreadcrumbsProps {
  breadcrumbs: Breadcrumb[];
}

export const FileBreadcrumbs: React.FC<FileBreadcrumbsProps> = ({
  breadcrumbs,
}) => {
  const { t } = useTranslation("files");
  const scrollRef = useRef<HTMLDivElement | null>(null);

  const filtered = useMemo(
    () =>
      breadcrumbs.filter((crumb, idx) => idx > 0 || crumb.name !== "Default"),
    [breadcrumbs],
  );

  const updateFade = () => {
    const el = scrollRef.current;
    if (!el) return;
    const canScroll = el.scrollWidth > el.clientWidth + 1;
    const shouldShow = canScroll && el.scrollLeft > 0;
    el.style.setProperty(
      "--ctn-bc-left-fade-opacity",
      shouldShow ? "0.9" : "0",
    );
  };

  useLayoutEffect(() => {
    // Scroll to the right (end) to show the current path.
    // RAF helps when fonts/layout settle after render.
    const el = scrollRef.current;
    if (!el) return;
    const id = requestAnimationFrame(() => {
      el.scrollLeft = el.scrollWidth;
      updateFade();
    });
    return () => cancelAnimationFrame(id);
  }, [filtered]);

  useEffect(() => {
    updateFade();
    const el = scrollRef.current;
    if (!el) return;

    const onScroll = () => updateFade();
    el.addEventListener("scroll", onScroll, { passive: true });

    const ro = new ResizeObserver(() => updateFade());
    ro.observe(el);

    return () => {
      el.removeEventListener("scroll", onScroll);
      ro.disconnect();
    };
  }, []);

  return (
    <Box
      ref={scrollRef}
      sx={{
        position: "relative",
        overflow: "auto",
        flex: 1,
        minWidth: 0,
        "--ctn-bc-left-fade-opacity": 0,
        "&::-webkit-scrollbar": { display: "none" },
        msOverflowStyle: "none",
        scrollbarWidth: "none",
        // Fade-out gradient on the left to indicate hidden content
        "&::before": {
          content: '""',
          position: "absolute",
          top: 0,
          left: 0,
          width: "40px",
          height: "100%",
          background: (theme) =>
            `linear-gradient(to right, ${theme.palette.background.default}, transparent)`,
          pointerEvents: "none",
          zIndex: 1,
          opacity: "var(--ctn-bc-left-fade-opacity)",
          transition: "opacity 120ms ease-out",
        },
      }}
    >
      <Breadcrumbs
        aria-label={t("breadcrumbs.ariaLabel")}
        sx={{
          whiteSpace: "nowrap",
          display: "inline-flex",
          width: "max-content",
          "& .MuiBreadcrumbs-ol": {
            flexWrap: "nowrap",
          },
          "& .MuiBreadcrumbs-li": {
            whiteSpace: "nowrap",
          },
        }}
      >
        {filtered.map((crumb, idx) => {
          const isLast = idx === filtered.length - 1;
          if (isLast) {
            return (
              <Typography
                key={crumb.id}
                color="text.primary"
                noWrap
                title={crumb.name}
              >
                {crumb.name}
              </Typography>
            );
          }
          return (
            <MuiLink
              key={crumb.id}
              component={RouterLink}
              underline="hover"
              color="inherit"
              to={`/files/${crumb.id}`}
              sx={{ fontSize: "1.1rem" }}
              noWrap
              title={crumb.name}
            >
              {crumb.name}
            </MuiLink>
          );
        })}
      </Breadcrumbs>
    </Box>
  );
};
