import { useMemo } from "react";
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

  const filtered = useMemo(
    () =>
      breadcrumbs.filter((crumb, idx) => idx > 0 || crumb.name !== "Default"),
    [breadcrumbs],
  );

  return (
    <Box
      sx={{
        width: "100%",
        overflow: "hidden",
        direction: "rtl",
      }}
    >
      <Breadcrumbs
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
