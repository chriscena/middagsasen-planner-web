<template>
  <q-badge v-if="file.acknowledgedAt" color="green" label="Kvittert"></q-badge>
  <q-badge v-else color="orange" label="Ulest"></q-badge>
  <q-badge
    v-if="file.isMandatory"
    outline
    color="grey-8"
    label="Påkrevd"
    class="q-ml-xs"
  ></q-badge>
</template>

<script setup lang="ts">
// Kvitteringsstatus for en instruks: «Ulest»/«Kvittert», og «Påkrevd» når
// brukeren må ha kvittert før vakt. Brukes i «Nyttig info» på oppgaven og på
// Instrukser-siden, så uttrykket er likt begge steder.
import type { FileInfoResponse } from "@/types";

defineProps<{
  file: Pick<FileInfoResponse, "acknowledgedAt" | "isMandatory">;
}>();
</script>
