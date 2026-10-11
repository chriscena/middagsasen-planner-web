<template>
  <div class="attachment-pdf-pages">
    <!-- pdf er undefined til dokumentet er lastet (og pageNumbers tom). Hver
         side har en egen wrapper som alltid er i DOM-en, så scroll-høyden og
         sentinelen under siste side er riktig fra start. Selve siden
         (canvas) rendres bare når wrapperen er nær viewporten. -->
    <template v-if="pdf">
      <div
        v-for="page in pageNumbers"
        :key="page"
        :ref="(el) => setPageElement(page, el)"
        :data-page="page"
        class="attachment-pdf-page q-mb-sm"
        :style="isRendered(page) ? undefined : placeholderStyle"
      >
        <VuePDF v-if="isRendered(page)" :pdf="pdf" :page="page" fit-parent />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
// Rendrer sidene i en PDF under hverandre, skalert til bredden på forelderen.
// Lastes lazy fra AttachmentViewerDialog (defineAsyncComponent), så pdf.js
// (stor) havner i en egen chunk og ikke i hovedbunten.
//
// Minimal-inngangen til @tato30/vue-pdf brukes: standardinngangen baker
// workeren inn som en base64 data-URL (dobler chunken). I stedet pekes pdf.js
// til Vites ?url-import av worker-skriptet, som blir en egen fil i bygget.
// Workeren må settes før usePDF kalles, og versjonen følger pdfjs-dist, som
// er pinnet til samme versjon som @tato30/vue-pdf bruker.
//
// Legacy-bygget av pdf.js brukes både her (worker) og for selve biblioteket
// («pdfjs-dist» er aliaset til legacy/build/pdf.mjs i quasar.config.ts), så
// PDF-visning virker på eldre iOS Safari (< 17.4 mangler bl.a.
// Promise.withResolvers). Workeren transpileres ikke av Vite, så den må være
// legacy selv.
import {
  computed,
  effectScope,
  onBeforeUnmount,
  ref,
  shallowRef,
  watch,
  type ComponentPublicInstance,
} from "vue";
import { GlobalWorkerOptions } from "pdfjs-dist";
import pdfWorkerUrl from "pdfjs-dist/legacy/build/pdf.worker.min.mjs?url";
import { VuePDF, usePDF, type PDFSrc } from "@tato30/vue-pdf/minimal";

GlobalWorkerOptions.workerSrc = pdfWorkerUrl;

// Sideforhold (høyde/bredde) for plassholdere til sideforholdet fra første
// side er målt: A4 stående.
const DEFAULT_ASPECT_RATIO = 297 / 210;

// Sider rendres når de er innenfor én skjermhøyde over/under viewporten, og
// fjernes igjen utenfor, så en lang PDF ikke holder alle canvasene i minnet.
const RENDER_ROOT_MARGIN = "100% 0px";

const props = defineProps<{
  // PDF-innholdet, hentet med innlogging av forelderen (fetchResourceTypeFile).
  blob: Blob;
}>();

const emit = defineEmits<{
  // Dokumentet er lastet og sideantallet kjent: alle side-wrapperne er i
  // DOM-en, så forelderen kan legge sentinelen under siste side.
  ready: [];
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

// Plassholder for sider som ikke er rendret: padding-bottom i prosent gir
// høyde ut fra bredden i alle nettlesere (aspect-ratio mangler i eldre iOS).
// Sideforholdet fra første side brukes for alle; blandede formater gir et
// lite avvik i plassholderhøyden, som rettes når siden rendres.
const aspectRatio = ref<number | null>(null);
const placeholderStyle = computed(() => ({
  paddingBottom: `${(aspectRatio.value ?? DEFAULT_ASPECT_RATIO) * 100}%`,
}));

// usePDF setter pages før pdf, så når pdf er satt, er sideantallet kjent.
watch(pdf, async (task) => {
  if (!task) return;
  if (pages.value === 0) {
    emit("error", new Error("PDF-en har ingen sider."));
    return;
  }
  emit("ready");
  try {
    const document = await task.promise;
    const viewport = (await document.getPage(1)).getViewport({ scale: 1 });
    aspectRatio.value = viewport.height / viewport.width;
  } catch (error) {
    // Plassholderne beholder standardforholdet; visningen virker likevel.
    console.error(error);
  }
});

// --- Lat rendering ---------------------------------------------------------
// Én IntersectionObserver for alle side-wrapperne (root = viewporten; dialogen
// er maximized, så den er lik scroll-området). Uten IntersectionObserver
// rendres alle sidene.
const supportsLazyRendering = typeof IntersectionObserver !== "undefined";
const renderedPages = ref(new Set<number>());
const pageElements = new Map<number, Element>();

function isRendered(page: number): boolean {
  return !supportsLazyRendering || renderedPages.value.has(page);
}

const pageObserver = supportsLazyRendering
  ? new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          const page = Number((entry.target as HTMLElement).dataset.page);
          if (entry.isIntersecting) renderedPages.value.add(page);
          else renderedPages.value.delete(page);
        }
      },
      { rootMargin: RENDER_ROOT_MARGIN }
    )
  : null;

// Funksjons-ref fra v-for: elementet når det monteres, null når det fjernes.
function setPageElement(
  page: number,
  element: Element | ComponentPublicInstance | null
): void {
  const previous = pageElements.get(page);
  if (previous === element) return;
  if (previous) {
    pageObserver?.unobserve(previous);
    pageElements.delete(page);
  }
  if (element instanceof Element) {
    pageElements.set(page, element);
    pageObserver?.observe(element);
  }
}

watch(
  () => props.blob,
  async (blob) => {
    renderedPages.value.clear();
    aspectRatio.value = null;
    try {
      src.value = new Uint8Array(await blob.arrayBuffer());
    } catch (error) {
      emit("error", error);
    }
  },
  { immediate: true }
);

// --- Opprydding ------------------------------------------------------------
// VuePDF har autoDestroy av som standard (flere sider deler dokumentet), så
// dokumentet og workeren frigjøres her. Lukkes viseren før dokumentet er
// ferdig lastet, er pdf fortsatt undefined ved unmount: usePDF setter pdf
// først når getDocument-løftet er innfridd, og den ignorerer src = undefined
// (composable.ts: `if (src.value) processLoadingTask(...)`), så å nullstille
// src frigjør ingenting. Derfor destrueres dokumentet i stedet når pdf
// senere blir satt, fra en frittstående effectScope som overlever unmount
// (komponentens egne watch-er stoppes ved unmount).
let unmounted = false;
const destroyScope = effectScope(true);
destroyScope.run(() => {
  watch(pdf, (task) => {
    if (!unmounted || !task) return;
    void task.destroy();
    destroyScope.stop();
  });
});

onBeforeUnmount(() => {
  unmounted = true;
  pageObserver?.disconnect();
  pageElements.clear();
  if (pdf.value) {
    void pdf.value.destroy();
    destroyScope.stop();
  }
});
</script>
