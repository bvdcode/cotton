import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

// https://vite.dev/config/
export default defineConfig(() => {
  return {
    server: {
      proxy: {
        "/api": {
          target: "https://cotton.belov.us",
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
        includeAssets: ["/icon.png", "/icon.svg"],
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
          categories: ["account", "management", "server"],
          lang: "en-US",
          scope: "/",
          start_url: "/",
          display: "fullscreen",
          background_color: "#101010",
          theme_color: "#202020",
          screenshots: [
            {
              src: "/assets/images/screenshot1.jpg",
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
