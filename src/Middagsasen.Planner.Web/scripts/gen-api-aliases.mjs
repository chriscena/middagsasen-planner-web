// Genererer to filer fra backendens OpenAPI-dokument. Kjøres som del av
// `npm run gen:api` (etter openapi-typescript, som skriver src/types/api.d.ts):
//
// - src/types/enums.ts: navngitte konstanter for enum-schemaer. Backend
//   serialiserer C#-enumer som heltall og beskriver dem med `enum` (verdiene)
//   og `x-enum-varnames` (navnene i samme rekkefølge).
// - src/types/index.ts: lesbare type-aliaser for alle schemaer; enumene
//   re-eksporteres fra ./enums.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const specPath = resolve(
  root,
  "../Middagsasen.Planner.Api/openapi/openapi.json"
);
const indexPath = resolve(root, "src/types/index.ts");
const enumsPath = resolve(root, "src/types/enums.ts");

const spec = JSON.parse(readFileSync(specPath, "utf8"));
const schemas = spec.components?.schemas ?? {};
const names = Object.keys(schemas).sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));

const identifier = /^[A-Za-z_$][A-Za-z0-9_$]*$/;

const invalid = names.filter((n) => !identifier.test(n));
if (invalid.length > 0) {
  throw new Error(`Schema-navn er ikke gyldige TS-identifikatorer: ${invalid}`);
}

const header = [
  "// Autogenerert av npm run gen:api — ikke rediger.",
  "// Kilde: src/Middagsasen.Planner.Api/openapi/openapi.json",
  "",
];

// Et schema regnes som enum når det har en `enum`-liste. Heltalls-enumer må ha
// `x-enum-varnames`, ellers finnes det ingen navn å generere konstanter fra.
// Et heltalls-schema uten properties og uten `enum` er sannsynligvis en enum
// der backend ikke har beskrevet verdiene — det regnes også som feil.
const enumNames = names.filter((n) => Array.isArray(schemas[n].enum));

for (const n of names) {
  const s = schemas[n];
  if (s.type === "integer" && !s.properties && !Array.isArray(s.enum)) {
    throw new Error(
      `Schema "${n}" er et heltall uten "enum"-liste. Sjekk at backend ` +
        `beskriver enumen i OpenAPI-dokumentet (EnumSchemaTransformer).`
    );
  }
}

const enumBlocks = enumNames.map((n) => {
  const s = schemas[n];
  const values = s.enum;
  const varnames = s["x-enum-varnames"];
  if (!Array.isArray(varnames)) {
    throw new Error(
      `Enum-schema "${n}" mangler "x-enum-varnames" — kan ikke generere ` +
        `navngitte verdier i enums.ts.`
    );
  }
  if (varnames.length !== values.length) {
    throw new Error(
      `Enum-schema "${n}": "x-enum-varnames" har ${varnames.length} navn, ` +
        `men "enum" har ${values.length} verdier.`
    );
  }
  const badNames = varnames.filter((v) => !identifier.test(v));
  if (badNames.length > 0) {
    throw new Error(
      `Enum-schema "${n}": navn er ikke gyldige TS-identifikatorer: ${badNames}`
    );
  }
  return [
    `/** Enum-schemaet \`${n}\` i OpenAPI-dokumentet. */`,
    `export const ${n} = {`,
    ...varnames.map((v, i) => `  ${v}: ${JSON.stringify(values[i])},`),
    `} as const satisfies Record<string, Schemas["${n}"]>;`,
    `export type ${n} = (typeof ${n})[keyof typeof ${n}];`,
  ].join("\n");
});

const enumsLines = [
  ...header,
  'import type { components } from "./api";',
  "",
  'type Schemas = components["schemas"];',
  "",
  enumBlocks.join("\n\n"),
  "",
];

const enumSet = new Set(enumNames);
const indexLines = [
  ...header,
  'import type { components } from "./api";',
  "",
  'type Schemas = components["schemas"];',
  "",
  ...names.map((n) =>
    enumSet.has(n)
      ? `export { ${n} } from "./enums";`
      : `export type ${n} = Schemas["${n}"];`
  ),
  "",
];

writeFileSync(enumsPath, enumsLines.join("\n"));
writeFileSync(indexPath, indexLines.join("\n"));
console.log(`Skrev ${enumNames.length} enumer til ${enumsPath}`);
console.log(`Skrev ${names.length} aliaser til ${indexPath}`);
