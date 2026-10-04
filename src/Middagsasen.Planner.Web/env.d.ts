/**
 * Typer som ikke legges til automatisk av Quasar CLI (QUASAR_*-variablene og
 * vite/client er allerede med via .quasar/quasar.d.ts).
 *
 * https://quasar.dev/quasar-cli-vite/handling-import-meta-env#type-inference
 *
 * @example
 * interface ImportMetaEnv {
 *   readonly MY_VAR: string;
 * }
 */
// oxlint-disable-next-line typescript/no-empty-object-type
interface ImportMetaEnv {}

// Definert i quasar.config.ts (build.extendViteConf) ved bygg.
declare const __APP_VERSION__: {
  version: string;
  builtAt: string;
  sha: string;
};
