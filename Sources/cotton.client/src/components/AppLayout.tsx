import React from "react";
import { Box } from "@mui/material";

const AppLayout: React.FC = () => {
  return (
    <Box
      sx={{
        height: "100%",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 2,
        textAlign: "center",
        p: 2,
      }}
    >
      App Layout
    </Box>
  );
};

export default AppLayout;
