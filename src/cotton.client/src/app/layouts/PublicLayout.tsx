import { Box } from "@mui/material";
import { Outlet } from "react-router-dom";

export const PublicLayout = () => {
  return (
    <Box
      width="100%"
      height="100%"
      display="flex"
      flexDirection="column"
      alignItems="stretch"
      justifyContent="flex-start"
      minHeight={0}
      minWidth={0}
    >
      <Box flex={1} minHeight={0} minWidth={0} overflow="auto">
        <Outlet />
      </Box>
    </Box>
  );
};
