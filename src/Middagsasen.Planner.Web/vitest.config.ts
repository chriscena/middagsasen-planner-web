import { defineConfig } from "vitest/config";
import { resolve } from "path";
import { fileURLToPath } from "node:url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));

// Testene kjører i norsk tidssone, slik at dato-/tidstester (midnatt og
// sommertid) er deterministiske og meningsfulle uansett maskin/CI.
process.env.TZ = "Europe/Oslo";

export default defineConfig({
  resolve: {
    alias: {
      src: resolve(__dirname, "src"),
      stores: resolve(__dirname, "src/stores"),
      boot: resolve(__dirname, "src/boot"),
    },
  },
  test: {
    environment: "node",
    include: ["src/**/*.{test,spec}.{js,ts}"],
    env: { TZ: "Europe/Oslo" },
  },
});
