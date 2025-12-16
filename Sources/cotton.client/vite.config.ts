import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig(() => {
  return {
    plugins: [react()],
    server: {
      proxy: {
        "/api": {
          target: "https://cotton.splidex.com",
          changeOrigin: true,
          secure: true,
          ws: true,
        },
      },
    },
  };
});
