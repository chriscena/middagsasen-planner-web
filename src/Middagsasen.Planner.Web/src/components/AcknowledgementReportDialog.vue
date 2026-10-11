<template>
  <q-dialog
    :model-value="modelValue"
    :maximized="$q.screen.lt.sm"
    @update:model-value="emit('update:modelValue', $event)"
    @before-show="onBeforeShow"
    @hide="reset"
  >
    <!-- Fullskjerm på mobil, ellers et vindu med høyde- og breddegrense der
         bare tabellen ruller. -->
    <q-card
      class="column no-wrap"
      :class="{ 'acknowledgement-report--windowed': !$q.screen.lt.sm }"
    >
      <q-toolbar class="bg-primary text-white">
        <q-btn flat round dense icon="close" title="Lukk" v-close-popup></q-btn>
        <q-toolbar-title class="ellipsis">{{
          file.description
        }}</q-toolbar-title>
      </q-toolbar>
      <q-card-section class="q-py-sm">
        <div class="text-subtitle1">
          <template v-if="report"
            >{{ report.acknowledgedCount }} av {{ report.total }} har
            kvittert</template
          >
          <template v-else>&nbsp;</template>
        </div>
        <div v-if="file.archived" class="text-caption text-grey-7">
          Arkivert {{ formatDate(file.archived) }}
        </div>
        <q-toggle
          v-model="showAll"
          label="Vis alle brukere"
          :disable="loading"
          @update:model-value="load"
        ></q-toggle>
      </q-card-section>
      <q-separator></q-separator>
      <div class="col scroll">
        <q-table
          flat
          dense
          wrap-cells
          row-key="userId"
          :rows="report?.users ?? []"
          :columns="columns"
          :loading="loading"
          :filter="filter"
          :filter-method="filterByName"
          :pagination="{ rowsPerPage: 0 }"
          hide-pagination
          no-data-label="Ingen brukere"
          no-results-label="Ingen brukere med det navnet"
        >
          <template #top>
            <q-input
              v-model="filter"
              class="full-width"
              dense
              outlined
              clearable
              debounce="200"
              placeholder="Søk på navn"
            >
              <template #prepend><q-icon name="search"></q-icon></template>
            </q-input>
          </template>
          <template #body-cell-fullName="cell">
            <q-td :props="cell">
              <div>{{ cell.row.fullName }}</div>
              <div
                v-if="showAll && !cell.row.appliesTo"
                class="text-caption text-grey-7"
              >
                Har ikke hatt vakten denne sesongen
              </div>
            </q-td>
          </template>
          <template #body-cell-acknowledgedAt="cell">
            <q-td :props="cell">
              <span v-if="cell.row.acknowledgedAt">{{
                formatDateTime(cell.row.acknowledgedAt)
              }}</span>
              <span v-else class="text-negative">Ikke kvittert</span>
            </q-td>
          </template>
        </q-table>
      </div>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
// Kvitteringsrapport for én instruks (aktiv eller arkivert), for admin:
// hvem som har kvittert, når og med hvilken kommentar. Som standard brukerne
// vakttypen gjelder for (samme populasjon som badgen brukerne ser), med en
// bryter for alle aktive brukere. Backend sorterer: ikke kvittert øverst.
import { ref } from "vue";
import { useQuasar, type QTableColumn } from "quasar";
import { useEventStore } from "@/stores/EventStore";
import { notifyApiError } from "@/shared/notifyApiError";
import { formatDate, formatDateTime } from "@/shared/time";
import type {
  AcknowledgementReportResponse,
  AcknowledgementReportUserResponse,
  FileInfoResponse,
} from "@/types";

const props = defineProps<{
  file: FileInfoResponse;
  resourceTypeId: number;
  modelValue: boolean;
}>();

const emit = defineEmits<{
  "update:modelValue": [value: boolean];
}>();

const $q = useQuasar();
const eventStore = useEventStore();

const columns: QTableColumn<AcknowledgementReportUserResponse>[] = [
  {
    name: "fullName",
    label: "Navn",
    field: "fullName",
    align: "left",
  },
  {
    name: "acknowledgedAt",
    label: "Kvittert",
    field: "acknowledgedAt",
    align: "left",
  },
  {
    name: "comment",
    label: "Kommentar",
    field: (row) => row.comment ?? "",
    align: "left",
  },
];

const report = ref<AcknowledgementReportResponse | null>(null);
const loading = ref(false);
const showAll = ref(false);
const filter = ref<string | null>(null);
// Løpenummer for siste henting; svar fra eldre hentinger (f.eks. bryteren
// slått frem og tilbake raskt) forkastes.
let loadSeq = 0;

function onBeforeShow() {
  showAll.value = false;
  filter.value = null;
  void load();
}

async function load() {
  const seq = ++loadSeq;
  loading.value = true;
  try {
    const data = await eventStore.getAcknowledgementReport(
      props.resourceTypeId,
      props.file.id,
      showAll.value
    );
    if (seq === loadSeq) report.value = data;
  } catch (error) {
    if (seq === loadSeq) {
      notifyApiError(error, "Klarte ikke å hente kvitteringsrapporten.");
    }
  } finally {
    if (seq === loadSeq) loading.value = false;
  }
}

function reset() {
  loadSeq += 1;
  report.value = null;
  loading.value = false;
}

// Filtrerer bare på navn (standardfilteret søker i alle kolonner).
function filterByName(
  rows: readonly AcknowledgementReportUserResponse[],
  terms: string
): readonly AcknowledgementReportUserResponse[] {
  const needle = terms.trim().toLocaleLowerCase("nb");
  if (!needle) return rows;
  return rows.filter((row) =>
    row.fullName.toLocaleLowerCase("nb").includes(needle)
  );
}
</script>

<style scoped>
.acknowledgement-report--windowed {
  width: 100%;
  max-width: 800px;
  max-height: 85vh;
}
</style>
