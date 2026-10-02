// Genererer src/types/index.ts med lesbare type-aliaser for alle schemaer i
// backendens OpenAPI-dokument. Kjøres som del av `npm run gen:api`.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const specPath = resolve(
  root,
  "../Middagsasen.Planner.Api/openapi/openapi.json"
);
const outPath = resolve(root, "src/types/index.ts");

const spec = JSON.parse(readFileSync(specPath, "utf8"));
const schemas = spec.components?.schemas ?? {};
const names = Object.keys(schemas).sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));

const invalid = names.filter((n) => !/^[A-Za-z_$][A-Za-z0-9_$]*$/.test(n));
if (invalid.length > 0) {
  throw new Error(`Schema-navn er ikke gyldige TS-identifikatorer: ${invalid}`);
}

// C#-enumer serialiseres som heltall uten verdiliste ({ "type": "integer" }).
// De har navngitte, håndskrevne verdier i ./enums og re-eksporteres derfra.
// Kommer det en ny enum i backend, feiler typecheck til den er lagt i enums.ts.
const isEnum = (name) =>
  schemas[name].type === "integer" && !schemas[name].properties;

const lines = [
  "// Autogenerert av npm run gen:api — ikke rediger.",
  "// Kilde: src/Middagsasen.Planner.Api/openapi/openapi.json",
  "",
  'import type { components } from "./api";',
  "",
  'type Schemas = components["schemas"];',
  "",
  ...names.map((n) =>
    isEnum(n)
      ? `export { ${n} } from "./enums";`
      : `export type ${n} = Schemas["${n}"];`
  ),
  "",
];

writeFileSync(outPath, lines.join("\n"));
console.log(`Skrev ${names.length} aliaser til ${outPath}`);
