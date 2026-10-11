<template>
  <div class="attachment-pdf-pages">
    <!-- pdf er undefined til dokumentet er lastet (og pageNumbers tom). -->
    <template v-if="pdf">
      <VuePDF
        v-for="page in pageNumbers"
        :key="page"
        :pdf="pdf"
        :page="page"
        fit-parent
        class="q-mb-sm"
        @loaded="onPageLoaded(page)"
      />
    </template>
  </div>
</template>

<script setup lang="ts">
// Rendrer alle sidene i en PDF under hverandre, skalert til bredden på
// forelderen. Lastes lazy fra AttachmentViewerDialog (defineAsyncComponent),
// så pdf.js (stor) havner i en egen chunk og ikke i hovedbunten.
//
// Minimal-inngangen til @tato30/vue-pdf brukes: standardinngangen baker
// workeren inn som en base64 data-URL (dobler chunken). I stedet pekes pdf.js
// til Vites ?url-import av worker-skriptet, som blir en egen fil i bygget.
// Workeren må settes før usePDF kalles, og versjonen følger pdfjs-dist som
// @tato30/vue-pdf drar inn, så de alltid er like.
//
// Legacy-bygget av pdf.js brukes både her (worker) og for selve biblioteket
// («pdfjs-dist» er aliaset til legacy/build/pdf.mjs i quasar.config.ts), så
// PDF-visning virker på eldre iOS Safari (< 17.4 mangler bl.a.
// Promise.withResolvers). Workeren transpileres ikke av Vite, så den må være
// legacy selv.
import { computed, onBeforeUnmount, shallowRef, watch } from "vue";
import { GlobalWorkerOptions } from "pdfjs-dist";
import pdfWorkerUrl from "pdfjs-dist/legacy/build/pdf.worker.min.mjs?url";
import { VuePDF, usePDF, type PDFSrc } from "@tato30/vue-pdf/minimal";

GlobalWorkerOptions.workerSrc = pdfWorkerUrl;

const props = defineProps<{
  // PDF-innholdet, hentet med innlogging av forelderen (fetchResourceTypeFile).
  blob: Blob;
}>();

const emit = defineEmits<{
  // Alle sidene er rendret; forelderen kan legge sentinelen under siste side.
  rendered: [];
  error: [error: unknown];
}>();

// usePDF tar imot en ref og laster dokumentet på nytt når den endres. Bytes
// (ikke object-URL), så pdf.js slipper å hente filen selv.
const src = shallowRef<PDFSrc>(undefined);
const { pdf, pages } = usePDF(src, {
  onError: (error: unknown) => emit("error", error),
});

const pageNumbers = computed(() =>
  Array.from({ length: pages.value }, (_, index) => index + 1)
);

// Sidene rendres uavhengig av hverandre; «rendered» sendes når alle er med.
const loadedPages = new Set<number>();
function onPageLoaded(page: number): void {
  loadedPages.add(page);
  if (pages.value > 0 && loadedPages.size >= pages.value) emit("rendered");
}

watch(
  () => props.blob,
  async (blob) => {
    loadedPages.clear();
    try {
      src.value = new Uint8Array(await blob.arrayBuffer());
    } catch (error) {
      emit("error", error);
    }
  },
  { immediate: true }
);

// VuePDF har autoDestroy av som standard (flere sider deler dokumentet), så
// dokumentet og workeren frigjøres her når viseren lukkes.
onBeforeUnmount(() => {
  pdf.value?.destroy();
});
</script>
