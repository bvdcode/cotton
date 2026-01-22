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
  const containerRef = useRef<HTMLDivElement | null>(null);
  const breadcrumbsRef = useRef<HTMLDivElement | null>(null);

  const filtered = useMemo(
    () =>
      breadcrumbs.filter((crumb, idx) => idx > 0 || crumb.name !== "Default"),
    [breadcrumbs],
  );

  const updateFade = () => {
    const container = containerRef.current;
    const bc = breadcrumbsRef.current;
    if (!container || !bc) return;

    const bcWidth = bc.getBoundingClientRect().width;
    const containerWidth = container.getBoundingClientRect().width;
    const isOverflowing = bcWidth > containerWidth + 1;

    container.style.setProperty(
      "--ctn-bc-left-fade",
      isOverflowing ? "0.9" : "0",
    );
  };

  useLayoutEffect(() => {
    const id = requestAnimationFrame(() => updateFade());
    return () => cancelAnimationFrame(id);
  }, [filtered]);

  useEffect(() => {
    updateFade();
    const container = containerRef.current;
    const bc = breadcrumbsRef.current;
    if (!container || !bc) return;

    const ro = new ResizeObserver(() => updateFade());
    ro.observe(container);
    ro.observe(bc);
    return () => ro.disconnect();
  }, []);

  return (
    <Box
      ref={containerRef}
      sx={{
        display: "flex",
        alignItems: "center",
        position: "relative",
        width: "100%",
        overflow: "hidden",
        direction: "rtl",
        "--ctn-bc-left-fade": 0,
        "&::before": {
          content: '""',
          position: "absolute",
          top: 0,
          left: 0,
          width: "60px",
          height: "100%",
          background: (theme) =>
            `linear-gradient(to right, ${theme.palette.background.default}, transparent)`,
          pointerEvents: "none",
          zIndex: 1,
          opacity: "var(--ctn-bc-left-fade)",
          transition: "opacity 200ms ease-out",
        },
      }}
    >
      <Breadcrumbs
        ref={breadcrumbsRef}
        aria-label={t("breadcrumbs.ariaLabel")}
        sx={{
          direction: "ltr",
          display: "inline-flex",
          whiteSpace: "nowrap",
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
