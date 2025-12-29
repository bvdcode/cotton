import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig(() => {
  return {
    plugins: [react()],
    server: {
      proxy: {
        "/api": {
          target: "http://localhost:5182",
          changeOrigin: true,
          secure: true,
          ws: true,
        },
      },
    },
  };
});
