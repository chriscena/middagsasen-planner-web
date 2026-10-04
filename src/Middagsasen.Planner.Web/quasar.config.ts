import { defineConfig } from "#q-app";
import type { Plugin } from "vite";
import { fileURLToPath } from "node:url";

// Versjons-ID for bygget. Brukes både i bundelen (__APP_VERSION__) og i
// version.json, slik at klienten kan oppdage at en ny versjon er deployet.
const builtAt = new Date().toISOString();
const sha = (process.env.GITHUB_SHA || "").slice(0, 7);
const version = sha ? `${builtAt}-${sha}` : builtAt;
const appVersion = { version, builtAt, sha };

// Skriver version.json til roten av output-mappen ved produksjonsbygg.
function versionJsonPlugin(): Plugin {
  return {
    name: "app-version-json",
    apply: "build",
    generateBundle() {
      this.emitFile({
        type: "asset",
        fileName: "version.json",
        source: JSON.stringify(appVersion),
      });
    },
  };
}

export default defineConfig((/* ctx */) => {
  return {
    // https://v2.quasar.dev/quasar-cli-vite/prefetch-feature
    // preFetch: true,

    // app boot file (/src/boot)
    // --> boot files are part of "main.js"
    // https://v2.quasar.dev/quasar-cli-vite/boot-files
    boot: [
      "i18n",
      "axios",
      "notify-defaults",
      "vuedatepicker",
      "version-check",
    ],

    // https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#css
    css: [
      "app.scss",
      // QCalendar 5: komponent-CSS-en definerer ikke variablene (borders, farger)
      // eller slide-transitions selv, så alle tre må med.
      "~@quasar/quasar-ui-qcalendar/QCalendarVariables.css",
      "~@quasar/quasar-ui-qcalendar/QCalendarTransitions.css",
      "~@quasar/quasar-ui-qcalendar/QCalendarAgenda.css",
    ],

    // https://github.com/quasarframework/quasar/tree/dev/extras
    extras: [
      // 'ionicons-v4',
      // 'mdi-v5',
      // 'fontawesome-v6',
      // 'eva-icons',
      // 'themify',
      // 'line-awesome',
      // 'roboto-font-latin-ext', // this or either 'roboto-font', NEVER both!

      "roboto-font", // optional, you are not bound to it
      "material-icons", // optional, you are not bound to it
    ],

    // Full list of options: https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#build
    build: {
      // https://v2.quasar.dev/quasar-cli-vite/quasar-config-file#build
      typescript: {
        strict: true,
        vueShim: true,
      },

      target: {
        browser: ["es2019", "edge88", "firefox78", "chrome87", "safari13.1"],
      },

      vueRouterMode: "history", // available values: 'hash', 'history'
      // vueRouterBase,
      // vueDevtools,
      // vueOptionsAPI: false,

      // rebuildCache: true, // rebuilds Vite/linter/etc cache on startup

      // publicPath: '/',
      // define: {},
      // defineEnv: {},
      // ignorePublicFolder: true,
      // minify: false,
      // distDir

      extendViteConf(viteConf) {
        viteConf.define = {
          ...viteConf.define,
          __APP_VERSION__: JSON.stringify(appVersion),
        };
      },
      // viteVuePluginOptions: {},

      vitePlugins: [
        [versionJsonPlugin, {}],
        [
          "@intlify/unplugin-vue-i18n/vite",
          {
            // if you want to use Vue I18n Legacy API, you need to set `compositionOnly: false`
            // compositionOnly: false,

            // if you want to use named tokens in your Vue I18n messages, such as 'Hello {name}',
            // you need to set `runtimeOnly: false`
            // runtimeOnly: false,

            // you need to set i18n resource including paths !
            include: [fileURLToPath(new URL("./src/i18n", import.meta.url))],
          },
        ],
      ],
    },

    // Full list of options: https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#devServer
    devServer: {
      // https: true
      proxy: {
        "/api": "http://[::1]:5239",
      },
      open: true, // opens browser window automatically
    },

    // https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#framework
    framework: {
      config: {},

      // iconSet: 'material-icons', // Quasar icon set
      lang: "nb-NO", // Quasar language pack

      // For special cases outside of where the auto-import strategy can have an impact
      // (like functional components as one of the examples),
      // you can manually specify Quasar components/directives to be available everywhere:
      //
      // components: [],
      // directives: [],

      // Quasar plugins
      plugins: ["Notify"],
    },

    // animations: 'all', // --- includes all animations
    // https://v2.quasar.dev/options/animations
    animations: [],

    // https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#property-sourcefiles
    // sourceFiles: {
    //   rootComponent: 'src/App.vue',
    //   router: 'src/router/index',
    //   store: 'src/stores/index',
    // },
  };
});
