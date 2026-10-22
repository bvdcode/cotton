import { Box, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";

interface AdminPageHeaderProps {
  title: ReactNode;
  description?: ReactNode;
  icon?: ReactNode;
  action?: ReactNode;
}

export const AdminPageHeader = ({
  title,
  description,
  icon,
  action,
}: AdminPageHeaderProps) => (
  <Stack
    direction={{ xs: "column", md: "row" }}
    spacing={1}
    sx={{
      justifyContent: "space-between",
      alignItems: { xs: "stretch", md: "center" },
    }}
  >
    <Stack
      direction="row"
      spacing={1.5}
      sx={{
        alignItems: "center",
        minWidth: 0,
      }}
    >
      {icon}
      <Stack
        spacing={0.5}
        sx={{
          minWidth: 0,
        }}
      >
        <Typography
          component="h1"
          variant="h5"
          sx={{
            fontWeight: 700,
          }}
        >
          {title}
        </Typography>
        {description && (
          <Typography
            variant="body2"
            sx={{
              color: "text.secondary",
            }}
          >
            {description}
          </Typography>
        )}
      </Stack>
    </Stack>
    {action && <Box>{action}</Box>}
  </Stack>
);
