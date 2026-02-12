import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiTarget = env.VITE_API_TARGET || "http://localhost:5182";

  return {
    server: {
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
        "^/s/[^/]+": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
        "/api/hub": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
        "/api/v1/hub": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
        "/hub": {
          target: apiTarget,
          changeOrigin: true,
          secure: true,
          ws: true,
        },
      },
    },
    plugins: [
      react(),
      VitePWA({
        registerType: "prompt",
        injectRegister: "auto",
        includeAssets: ["/favicon.ico", "/assets/icons/icon.svg"],
        workbox: {
          skipWaiting: true,
          clientsClaim: true,
          sourcemap: true,
          cleanupOutdatedCaches: true,
          // Allow larger bundles to be precached in CI (default is 2 MiB)
          maximumFileSizeToCacheInBytes: 6 * 1024 * 1024,
        },
        manifest: {
          id: "/",
          name: "Cotton Cloud",
          short_name: "Cotton Cloud",
          description: "Fast and reliable cloud service for your needs.",
          categories: ["cloud", "storage", "productivity"],
          lang: "en-US",
          scope: "/",
          start_url: "/",
          display: "standalone",
          background_color: "#2c2d2e",
          theme_color: "#c6ff00",
          screenshots: [
            {
              src: "/assets/images/screenshot1.jpg",
              sizes: "720x1280",
              type: "image/jpeg",
              form_factor: "narrow",
            },
            {
              src: "/assets/images/screenshot3.jpg",
              sizes: "720x1280",
              type: "image/jpeg",
              form_factor: "narrow",
            },
            {
              src: "/assets/images/screenshot5.jpg",
              sizes: "720x1280",
              type: "image/jpeg",
              form_factor: "narrow",
            },
            {
              src: "/assets/images/screenshot2.jpg",
              sizes: "1920x1080",
              type: "image/jpeg",
              form_factor: "wide",
            },
          ],
          icons: [
            {
              src: "/assets/icons/icon.svg",
              sizes: "any",
              type: "image/svg+xml",
              purpose: "any",
            },
            {
              src: "/assets/icons/icon-192.png",
              sizes: "192x192",
              type: "image/png",
              purpose: "any",
            },
            {
              src: "/assets/icons/icon-512.png",
              sizes: "512x512",
              type: "image/png",
              purpose: "any",
            },
            {
              src: "/assets/icons/icon-maskable-192.png",
              sizes: "192x192",
              type: "image/png",
              purpose: "maskable",
            },
            {
              src: "/assets/icons/icon-maskable-512.png",
              sizes: "512x512",
              type: "image/png",
              purpose: "maskable",
            },
            {
              src: "/assets/icons/icon-monochrome.svg",
              sizes: "512x512",
              type: "image/svg+xml",
              purpose: "monochrome",
            },
          ],
        },
      }),
    ],
  };
});
