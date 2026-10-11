<template>
  <q-dialog
    :model-value="modelValue"
    maximized
    @update:model-value="emit('update:modelValue', $event)"
    @before-show="load"
    @hide="reset"
  >
    <q-card class="column no-wrap">
      <q-toolbar class="bg-primary text-white">
        <q-btn flat round dense icon="close" title="Lukk" v-close-popup></q-btn>
        <q-toolbar-title class="ellipsis">{{
          file.description
        }}</q-toolbar-title>
        <q-badge
          v-if="file.isMandatory"
          outline
          color="white"
          label="Påkrevd"
          class="q-mr-sm"
        ></q-badge>
        <q-btn
          flat
          round
          dense
          icon="download"
          title="Last ned"
          :disable="loading"
          @click="download"
        ></q-btn>
      </q-toolbar>
      <div ref="scrollArea" class="col scroll bg-grey-3">
        <div v-if="loading" class="flex flex-center q-pa-xl">
          <q-spinner size="3em" color="primary"></q-spinner>
        </div>
        <q-banner
          v-else-if="errorMessage"
          rounded
          class="bg-negative text-white q-ma-md"
          >{{ errorMessage }}</q-banner
        >
        <template v-else-if="blob">
          <AttachmentPdfPages
            v-if="kind === 'pdf'"
            :blob="blob"
            class="q-pa-sm"
            @ready="onContentReady"
            @error="onContentError"
          ></AttachmentPdfPages>
          <q-img
            v-else-if="kind === 'image' && objectUrl"
            :src="objectUrl"
            fit="contain"
            class="full-width"
            :alt="file.description"
            @load="onContentReady"
            @error="onContentError"
          ></q-img>
          <!-- Andre filtyper kan ikke vises; nedlasting regnes som lest. -->
          <div
            v-else
            class="column flex-center q-pa-xl q-gutter-y-md text-center"
          >
            <q-icon name="description" size="3em" color="grey-7"></q-icon>
            <div>
              Vedlegget kan ikke vises her. Last det ned for å lese det.
            </div>
            <q-btn
              unelevated
              color="primary"
              no-caps
              icon="download"
              label="Last ned"
              @click="download"
            ></q-btn>
          </div>
          <!-- Sentinel under siste side: når den har vært synlig, er bunnen
               nådd (se shared/readTracking.ts). Rendres når sideantallet er
               kjent, så den ligger under siste side fra start. -->
          <div
            v-if="kind === 'pdf' && contentReady"
            ref="sentinel"
            class="attachment-viewer-sentinel"
          ></div>
        </template>
      </div>
      <template v-if="file.requiresAcknowledgement">
        <q-separator></q-separator>
        <q-card-section
          v-if="acknowledgedAt"
          class="row items-center no-wrap q-gutter-sm"
        >
          <q-icon name="check_circle" color="positive" size="sm"></q-icon>
          <div>
            <div>Kvittert {{ formatDate(acknowledgedAt) }}</div>
            <div v-if="savedComment" class="text-caption text-grey-8">
              {{ savedComment }}
            </div>
          </div>
        </q-card-section>
        <q-card-section v-else class="q-gutter-y-sm">
          <q-input
            outlined
            dense
            autogrow
            label="Kommentar (valgfritt)"
            v-model="comment"
            :maxlength="COMMENT_MAX_LENGTH"
            counter
            :disable="acknowledging"
          ></q-input>
          <q-btn
            class="full-width"
            unelevated
            color="primary"
            no-caps
            label="Jeg har lest og forstått instruksen"
            :disable="!hasReachedEnd"
            :loading="acknowledging"
            @click="acknowledge"
          ></q-btn>
          <div
            v-if="!hasReachedEnd"
            class="text-caption text-grey-7 text-center"
          >
            {{
              kind === "other"
                ? "Last ned vedlegget for å kvittere"
                : "Bla til bunnen for å kvittere"
            }}
          </div>
        </q-card-section>
      </template>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
// Fullskjermviser for vedlegg på en vakttype: PDF (alle sider under
// hverandre), bilde, eller nedlasting for andre filtyper. For instrukser
// (requiresAcknowledgement) har den en fotlinje for kvittering, som aktiveres
// når bunnen av dokumentet har vært synlig (bilder regnes som lest straks,
// andre filtyper når nedlastingen er startet).
import {
  computed,
  defineAsyncComponent,
  onBeforeUnmount,
  ref,
  watch,
} from "vue";
import { useQuasar } from "quasar";
import { useInstructionStore } from "@/stores/InstructionStore";
import { getApiErrorMessage } from "@/shared/apiError";
import {
  DOWNLOAD_ERROR_FALLBACK,
  downloadResourceTypeFileOrNotify,
  fetchResourceTypeFile,
  saveBlob,
} from "@/shared/fileDownload";
import { notifyApiError } from "@/shared/notifyApiError";
import { attachmentKind, reachedEnd } from "@/shared/readTracking";
import { formatDate } from "@/shared/time";
import type { FileInfoResponse } from "@/types";

// Speiler maks lengde på AcknowledgeRequest.Comment i backend.
const COMMENT_MAX_LENGTH = 1000;

const VIEW_ERROR_MESSAGE =
  "Klarte ikke å vise vedlegget. Last det ned i stedet.";

const props = defineProps<{
  file: FileInfoResponse;
  resourceTypeId: number;
  modelValue: boolean;
}>();

const emit = defineEmits<{
  "update:modelValue": [value: boolean];
  // Kvitteringen er registrert; filen med acknowledgedAt fra serveren.
  acknowledged: [file: FileInfoResponse];
}>();

const $q = useQuasar();
const instructionStore = useInstructionStore();

// pdf.js er stor, så PDF-sidene lastes først når en PDF faktisk åpnes. Feiler
// lastingen av chunken (nett, ny deploy), vises feilmeldingen; nedlasting
// virker fortsatt siden innholdet alt er hentet.
const AttachmentPdfPages = defineAsyncComponent({
  loader: () => import("@/components/AttachmentPdfPages.vue"),
  onError(error, _retry, fail) {
    onContentError(error);
    fail();
  },
});

const scrollArea = ref<HTMLElement | null>(null);
const sentinel = ref<HTMLElement | null>(null);

const loading = ref(false);
const errorMessage = ref<string | null>(null);
const blob = ref<Blob | null>(null);
// Object-URL for bilder (q-img trenger en src); frigjøres i reset.
const objectUrl = ref<string | null>(null);
const contentReady = ref(false);
const hasReachedEnd = ref(false);

const comment = ref("");
const acknowledging = ref(false);
// Kvitteringen gjort i denne dialogen, til props.file er oppdatert av
// forelderen. Kommentaren finnes ikke i FileInfoResponse, så den vises bare
// for kvitteringen som nettopp ble gjort her.
const acknowledgedFile = ref<FileInfoResponse | null>(null);
const savedComment = ref<string | null>(null);

const kind = computed(() =>
  attachmentKind(props.file.mimeType, props.file.fileName)
);
const acknowledgedAt = computed(
  () => acknowledgedFile.value?.acknowledgedAt ?? props.file.acknowledgedAt
);

// Løpenummer for hentingen. Lukkes dialogen (reset) eller åpnes den på nytt
// mens en henting pågår, er svaret fra den gamle hentingen uaktuelt: både
// innhold og feil forkastes. Object-URL lages først etter sjekken, så ingen
// URL fra en forkastet henting blir liggende.
let loadToken = 0;

async function load(): Promise<void> {
  reset();
  const token = ++loadToken;
  loading.value = true;
  let fetched: Blob;
  try {
    fetched = await fetchResourceTypeFile(props.file);
  } catch (error) {
    if (token !== loadToken) return;
    console.error(error);
    errorMessage.value = getApiErrorMessage(error, DOWNLOAD_ERROR_FALLBACK);
    loading.value = false;
    return;
  }
  if (token !== loadToken) return;
  blob.value = fetched;
  if (kind.value === "image") {
    objectUrl.value = URL.createObjectURL(fetched);
  }
  loading.value = false;
}

function reset(): void {
  loadToken++;
  disconnectObserver();
  if (objectUrl.value) URL.revokeObjectURL(objectUrl.value);
  objectUrl.value = null;
  blob.value = null;
  loading.value = false;
  errorMessage.value = null;
  contentReady.value = false;
  hasReachedEnd.value = false;
  comment.value = "";
  acknowledgedFile.value = null;
  savedComment.value = null;
}

function onContentReady(): void {
  contentReady.value = true;
  // Bilder regnes som lest straks de er vist; PDF-er når sentinelen har vært
  // synlig.
  if (kind.value === "image") hasReachedEnd.value = true;
}

function onContentError(error: unknown): void {
  console.error(error);
  errorMessage.value = VIEW_ERROR_MESSAGE;
}

// Sentinelen rendres først når sideantallet er kjent (v-if), så observatøren
// settes opp når template-ref-en får et element.
let observer: IntersectionObserver | null = null;
function disconnectObserver(): void {
  observer?.disconnect();
  observer = null;
}
watch(sentinel, (element) => {
  disconnectObserver();
  if (!element) return;
  if (typeof IntersectionObserver === "undefined") {
    // Uten IntersectionObserver kan ikke bunnen spores; sperr ikke kvittering.
    hasReachedEnd.value = true;
    return;
  }
  observer = new IntersectionObserver(
    (entries) => {
      hasReachedEnd.value = reachedEnd(hasReachedEnd.value, entries);
    },
    { root: scrollArea.value }
  );
  observer.observe(element);
});
onBeforeUnmount(() => {
  reset();
});

// Nedlasting gjenbruker innholdet som alt er hentet; ellers hentes det på
// nytt. Filtyper som ikke kan vises, regnes som lest når nedlastingen er
// startet.
async function download(): Promise<void> {
  if (blob.value) {
    saveBlob(blob.value, props.file.fileName);
  } else {
    await downloadResourceTypeFileOrNotify(props.file);
  }
  if (kind.value === "other") hasReachedEnd.value = true;
}

async function acknowledge(): Promise<void> {
  try {
    acknowledging.value = true;
    const result = await instructionStore.acknowledge(
      props.resourceTypeId,
      props.file.id,
      comment.value
    );
    savedComment.value = comment.value.trim() || null;
    acknowledgedFile.value = result;
    $q.notify({ type: "positive", message: "Kvitteringen er registrert." });
    emit("acknowledged", result);
  } catch (error) {
    notifyApiError(error, "Klarte ikke å registrere kvitteringen.");
  } finally {
    acknowledging.value = false;
  }
}
</script>

<style scoped>
/* 1px høy, så IntersectionObserver alltid har et areal å treffe. */
.attachment-viewer-sentinel {
  height: 1px;
}
</style>
