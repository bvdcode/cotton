// no props for now
import { Link, Outlet } from "react-router-dom";
import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Container from "@mui/material/Container";

const AppLayout = () => {
  return (
    <Box sx={{ display: "flex", flexDirection: "column", height: "100%" }}>
      <AppBar position="static" color="default" elevation={1}>
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            Cotton
          </Typography>
          <Stack direction="row" spacing={1}>
            <Button component={Link} to="/app" color="inherit">
              Home
            </Button>
            <Button component={Link} to="/app/dashboard" color="inherit">
              Dashboard
            </Button>
            <Button component={Link} to="/app/files" color="inherit">
              Files
            </Button>
            <Button component={Link} to="/app/options" color="inherit">
              Options
            </Button>
          </Stack>
        </Toolbar>
      </AppBar>
      <Container maxWidth="lg" sx={{ py: 2, flex: 1, overflow: "auto" }}>
        <Outlet />
      </Container>
    </Box>
  );
};

export default AppLayout;
