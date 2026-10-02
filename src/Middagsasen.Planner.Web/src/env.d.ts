/// <reference types="vite/client" />

// Definert i quasar.config.ts (build.extendViteConf) ved bygg.
declare const __APP_VERSION__: {
  version: string;
  builtAt: string;
  sha: string;
};
