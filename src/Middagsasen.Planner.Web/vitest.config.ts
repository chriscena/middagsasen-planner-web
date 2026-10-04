import { defineConfig } from "vitest/config";
import { resolve } from "path";
import { fileURLToPath } from "node:url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));

export default defineConfig({
  resolve: {
    alias: {
      "@": resolve(__dirname, "src"),
    },
  },
  test: {
    environment: "node",
    include: ["src/**/*.{test,spec}.{js,ts}"],
    // Testene kjører i norsk tidssone, slik at dato-/tidstester (midnatt og
    // sommertid) er deterministiske og meningsfulle uansett maskin/CI.
    env: { TZ: "Europe/Oslo" },
  },
});
