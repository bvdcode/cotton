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
    display="flex"
    justifyContent="center"
    alignItems="center"
    sx={{ mt: 1.5, textAlign: "center" }}
  >
    <Link
      href="https://github.com/bvdcode/cotton"
      target="_blank"
      rel="noopener"
      underline="hover"
      color="text.secondary"
      sx={{ display: "flex", alignItems: "center" }}
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
      color="text.secondary"
      sx={{
        pointerEvents: disabled ? "none" : "auto",
        opacity: disabled ? 0.5 : 1,
      }}
    >
      {label}
    </Link>
  </Box>
);
