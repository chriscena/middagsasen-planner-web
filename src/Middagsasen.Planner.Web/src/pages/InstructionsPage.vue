<template>
  <q-page padding>
    <q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="arrow_back"
          @click="$router.go(-1)"
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Instrukser</q-toolbar-title>
        <q-space></q-space>
        <ProfileMenuButton @click="emit('toggle-right')"></ProfileMenuButton>
      </q-toolbar>
    </q-header>
    <div v-if="showSpinner" class="flex flex-center q-pa-xl">
      <q-spinner size="3em" color="primary"></q-spinner>
    </div>
    <q-banner
      v-else-if="loadFailed && groups.length === 0"
      rounded
      class="bg-negative text-white"
    >
      Klarte ikke å hente instruksene.
      <template #action>
        <q-btn flat no-caps label="Prøv igjen" @click="load"></q-btn>
      </template>
    </q-banner>
    <div v-else-if="isEmpty" class="q-mt-xl text-center text-grey-8">
      Ingen instrukser å lese.
    </div>
    <q-list v-else>
      <template v-for="section in sections" :key="section.key">
        <q-item-label header>{{ section.header }}</q-item-label>
        <q-expansion-item
          v-for="group in section.groups"
          :key="group.resourceTypeId"
          :label="group.resourceTypeName"
          :caption="acknowledgementCaption(group)"
          :default-opened="section.opened"
          expand-separator
        >
          <q-list>
            <q-item
              v-for="file in group.files"
              :key="file.id"
              clickable
              v-ripple
              @click="openAttachment(file)"
            >
              <q-item-section>
                <q-item-label lines="1">{{ file.description }}</q-item-label>
                <q-item-label caption>
                  <InstructionStatusBadges
                    :file="file"
                  ></InstructionStatusBadges>
                </q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-icon name="chevron_right"></q-icon>
              </q-item-section>
            </q-item>
          </q-list>
        </q-expansion-item>
      </template>
    </q-list>
    <!-- Én viser for valgt instruks. -->
    <AttachmentViewerDialog
      v-if="selectedFile"
      v-model="showingAttachment"
      :file="selectedFile"
      :resource-type-id="selectedFile.resourceTypeId"
      @acknowledged="onAcknowledged"
    ></AttachmentViewerDialog>
  </q-page>
</template>

<script setup lang="ts">
// Alle instrukser gruppert per vakttype: «Gjelder deg» (vakttyper brukeren
// har vakt på) først og åpne, «Andre vakttyper» lukket. Klikk åpner viseren;
// kvittering oppdaterer store-en, så listen og badgene følger med.
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import AttachmentViewerDialog from "@/components/AttachmentViewerDialog.vue";
import InstructionStatusBadges from "@/components/InstructionStatusBadges.vue";
import ProfileMenuButton from "@/components/ProfileMenuButton.vue";
import { useInstructionStore } from "@/stores/InstructionStore";
import {
  acknowledgementCaption,
  groupInstructions,
} from "@/shared/instructionList";
import { notifyApiError } from "@/shared/notifyApiError";
import type { FileInfoResponse, InstructionGroupResponse } from "@/types";

interface Section {
  key: "mine" | "others";
  header: string;
  groups: InstructionGroupResponse[];
  // Vakttypene i seksjonen er åpne ved første visning.
  opened: boolean;
}

const emit = defineEmits<{ "toggle-right": [] }>();
const $router = useRouter();
const instructionStore = useInstructionStore();

// En henting er fullført (vellykket). Tom tilstand vises først da, så en
// feilet henting ikke ser ut som «ingen instrukser».
const loaded = ref(false);
// Siste henting feilet. Uten noe å vise fra før gir det banner med «Prøv
// igjen»; med en liste fra før (f.eks. fra MainLayout) bare et varsel.
const loadFailed = ref(false);
const selectedFile = ref<FileInfoResponse | null>(null);
const showingAttachment = ref(false);

const groups = computed(() => instructionStore.groups);
const showSpinner = computed(
  () => instructionStore.loading && groups.value.length === 0
);
const isEmpty = computed(() => loaded.value && groups.value.length === 0);

// Seksjoner uten innhold vises ikke (heller ikke overskriften).
const sections = computed((): Section[] => {
  const split = groupInstructions(groups.value);
  const all: Section[] = [
    { key: "mine", header: "Gjelder deg", groups: split.mine, opened: true },
    {
      key: "others",
      header: "Andre vakttyper",
      groups: split.others,
      opened: false,
    },
  ];
  return all.filter((section) => section.groups.length > 0);
});

// Store-en deler en pågående henting, så ved direkte oppstart gir dette og
// MainLayout sin henting til sammen én GET.
async function load(): Promise<void> {
  loadFailed.value = false;
  try {
    await instructionStore.getMyInstructions();
    loaded.value = true;
  } catch (error) {
    loadFailed.value = true;
    if (groups.value.length > 0) {
      notifyApiError(error, "Klarte ikke å hente instruksene.");
    }
  }
}

onMounted(load);

function openAttachment(file: FileInfoResponse): void {
  selectedFile.value = file;
  showingAttachment.value = true;
}

// Store-en har alt hentet listen på nytt; viseren får filen med
// kvitteringstidspunkt så fotlinjen viser «Kvittert».
function onAcknowledged(file: FileInfoResponse): void {
  selectedFile.value = file;
}
</script>
