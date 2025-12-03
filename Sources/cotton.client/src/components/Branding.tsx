import { Avatar, Box, Typography } from "@mui/material";
import defaultLogoUrl from "../assets/default-logo.svg";

export type BrandingProps = {
  appName: string;
  logoUrl?: string;
  onClick: () => void;
};

export default function Branding({ appName, logoUrl, onClick }: BrandingProps) {
  return (
    <Box
      display="flex"
      alignItems="center"
      justifyContent="flex-start"
      gap={1}
      onClick={onClick}
      sx={{ cursor: "pointer" }}
    >
      <Avatar src={logoUrl ?? defaultLogoUrl} alt={appName} />
      <Typography
        variant="h6"
        component="div"
        noWrap
        sx={{
          display: { xs: "none", sm: "block" },
        }}
      >
        {appName}
      </Typography>
    </Box>
  );
}
