import {
  Box,
  Link,
  Paper,
  Alert,
  Button,
  Checkbox,
  TextField,
  Typography,
  IconButton,
  InputAdornment,
  FormControlLabel,
  CircularProgress,
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import { useState, type FormEvent, type ChangeEvent } from "react";

const Login: React.FC = () => {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const validate = () => {
    if (!email) return "Email is required";
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!re.test(email)) return "Invalid email format";
    if (!password) return "Password is required";
    if (password.length < 6) return "Password must be at least 6 characters";
    return null;
  };

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    const v = validate();
    if (v) {
      setError(v);
      return;
    }
    setLoading(true);
    try {
      await new Promise((res) => setTimeout(res, 800));
      console.log("login:", { email, password: "***", remember });
      setSuccess(true);
    } catch {
      setError("Unable to sign in. Please try again later.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        bgcolor: "#f3f4f6",
        p: 2,
      }}
    >
      <Paper
        component="form"
        onSubmit={handleSubmit}
        aria-labelledby="login-heading"
        sx={{ width: 360, p: 3, borderRadius: 2 }}
      >
        <Typography id="login-heading" variant="h6" gutterBottom>
          Sign in
        </Typography>

        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Enter your credentials to access your account
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {success && (
          <Alert severity="success" sx={{ mb: 2 }}>
            Signed in successfully.
          </Alert>
        )}

        <TextField
          autoFocus
          label="Email"
          type="email"
          value={email}
          onChange={(e: ChangeEvent<HTMLInputElement>) =>
            setEmail(e.target.value)
          }
          fullWidth
          required
          size="small"
          margin="normal"
        />

        <TextField
          label="Password"
          type={showPassword ? "text" : "password"}
          value={password}
          onChange={(e: ChangeEvent<HTMLInputElement>) =>
            setPassword(e.target.value)
          }
          fullWidth
          required
          size="small"
          margin="normal"
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton
                  aria-label={showPassword ? "hide password" : "show password"}
                  onClick={() => setShowPassword((s) => !s)}
                  edge="end"
                >
                  {showPassword ? <VisibilityOff /> : <Visibility />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />

        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            mt: 1,
          }}
        >
          <FormControlLabel
            control={
              <Checkbox
                checked={remember}
                onChange={(e) => setRemember(e.target.checked)}
              />
            }
            label="Remember me"
            sx={{ mr: 0 }}
          />
          <Link href="#" onClick={(e) => e.preventDefault()} underline="none">
            Forgot password?
          </Link>
        </Box>

        <Button
          type="submit"
          variant="contained"
          color="primary"
          fullWidth
          disabled={loading || success}
          sx={{ mt: 2, py: 1.1 }}
        >
          {loading ? (
            <CircularProgress size={20} color="inherit" />
          ) : success ? (
            "Done"
          ) : (
            "Sign in"
          )}
        </Button>
      </Paper>
    </Box>
  );
};

export default Login;
