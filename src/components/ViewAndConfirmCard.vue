<template>
  <q-card>
    <q-card-section> </q-card-section>
    <q-card-section>
      <VuePDF :pdf="pdf"></VuePDF>
    </q-card-section>
    <q-card-actions>
      <q-btn></q-btn>
      <q-space></q-space>
      <q-btn></q-btn>
    </q-card-actions>
  </q-card>
</template>

<script setup>
import { VuePDF, usePDF } from "@tato30/vue-pdf";

const props = defineProps({
  src: {
    type: String,
    required: true,
  },
});

const pdf = usePDF(src);
</script>
