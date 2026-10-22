import { Box, Divider, Link } from "@mui/material";
import { GitHub } from "@mui/icons-material";

interface ForgotPasswordLinkProps {
  onClick: () => void;
  disabled: boolean;
  label: string;
}

export const ForgotPasswordLink = ({
  onClick,
  disabled,
  label,
}: ForgotPasswordLinkProps) => (
  <Box
    sx={{
      display: "flex",
      justifyContent: "center",
      alignItems: "center",
      mt: 1.5,
      textAlign: "center",
    }}
  >
    <Link
      href="https://github.com/bvdcode/cotton"
      target="_blank"
      rel="noopener"
      underline="hover"
      sx={{
        color: "text.secondary",
        display: "flex",
        alignItems: "center",
      }}
    >
      <GitHub fontSize="small" sx={{ mr: 0.5 }} />
    </Link>
    <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />
    <Link
      component="button"
      type="button"
      variant="caption"
      onClick={onClick}
      underline="hover"
      sx={{
        color: "text.secondary",
        pointerEvents: disabled ? "none" : "auto",
        opacity: disabled ? 0.5 : 1,
      }}
    >
      {label}
    </Link>
  </Box>
);
