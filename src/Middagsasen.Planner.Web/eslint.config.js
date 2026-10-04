// ESLint brukes KUN for template-linting i .vue-filer (eslint-plugin-vue).
// All annen linting (JS/TS og <script>-delen av .vue) gjøres av oxlint, se .oxlintrc.json.
//
// Hvorfor ikke oxlint jsPlugins? oxlints JS-plugin-støtte (alpha) kan ikke bruke
// en egendefinert parser, så eslint-plugin-vue får ikke template-AST fra
// vue-eslint-parser og alle template-regler feiler med
// "Use the latest vue-eslint-parser".
import pluginVue from "eslint-plugin-vue";
import tsParser from "@typescript-eslint/parser";

// Regler fra flat/essential som oxlints innebygde vue-plugin allerede dekker.
// Slås av her for å unngå doble funn.
const coveredByOxlint = [
  "vue/no-arrow-functions-in-watch",
  "vue/no-async-in-computed-properties",
  "vue/no-computed-properties-in-data",
  "vue/no-deprecated-data-object-declaration",
  "vue/no-deprecated-delete-set",
  "vue/no-deprecated-destroyed-lifecycle",
  "vue/no-deprecated-events-api",
  "vue/no-deprecated-model-definition",
  "vue/no-deprecated-props-default-this",
  "vue/no-deprecated-vue-config-keycodes",
  "vue/no-dupe-keys",
  "vue/no-export-in-script-setup",
  "vue/no-expose-after-await",
  "vue/no-lifecycle-after-await",
  "vue/no-reserved-component-names",
  "vue/no-reserved-keys",
  "vue/no-reserved-props",
  "vue/no-shared-component-data",
  "vue/no-side-effects-in-computed-properties",
  "vue/no-watch-after-await",
  "vue/prefer-import-from-vue",
  "vue/require-prop-type-constructor",
  "vue/require-render-return",
  "vue/require-slots-as-functions",
  "vue/return-in-computed-property",
  "vue/return-in-emits-validator",
  "vue/valid-define-emits",
  "vue/valid-define-options",
  "vue/valid-define-props",
  "vue/valid-next-tick",
];

export default [
  {
    ignores: [
      "dist/**",
      ".quasar/**",
      "node_modules/**",
      "src-capacitor/**",
      "src-cordova/**",
    ],
  },
  ...pluginVue.configs["flat/essential"].map((config) => ({
    ...config,
    files: ["**/*.vue"],
  })),
  {
    files: ["**/*.vue"],
    languageOptions: {
      parserOptions: {
        parser: tsParser,
        ecmaVersion: "latest",
        sourceType: "module",
      },
    },
    rules: Object.fromEntries(coveredByOxlint.map((rule) => [rule, "off"])),
  },
];
