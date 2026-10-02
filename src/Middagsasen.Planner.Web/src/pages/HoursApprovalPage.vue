<template>
  <q-page padding>
    <q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="menu"
          @click="emit('toggle-left')"
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Time-godkjenning</q-toolbar-title>
        <q-space></q-space>
        <q-btn
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
          title="Din brukerinfo"
        ></q-btn>
      </q-toolbar>
    </q-header>
    <div>
      <q-table
        v-if="isAdmin && seasonsLoaded"
        :grid="$q.screen.lt.md"
        :bordered="$q.screen.lt.md"
        flat
        row-key="workHourId"
        ref="tableRef"
        :columns="columns"
        :visible-columns="visibleColumns"
        :loading="loading"
        :rows="userWorkHours"
        style="max-height: 85vh"
        no-data-label="Ingen data"
        class="sticky-header-table"
        v-model:pagination="pagination"
        @request="getUserWorkHours"
        selection="multiple"
        v-model:selected="selectedWorkHours"
        :filter="filter"
        @row-click="(_, row) => openWorkHours(row)"
      >
        <template #top>
          <div class="col-12 row q-col-gutter-sm q-pb-sm">
            <div :class="$q.screen.lt.md ? 'col-12' : 'col-3'">
              <q-select
                dense
                outlined
                label="Sesong"
                :disable="loading"
                :model-value="seasonFilter"
                :options="seasonStore.seasons"
                option-label="label"
                option-value="startYear"
                emit-value
                map-options
                @update:model-value="(val) => setFilter({ season: val })"
              />
            </div>
            <div :class="$q.screen.lt.md ? 'col-12' : 'col-3'">
              <q-select
                dense
                outlined
                clearable
                use-input
                input-debounce="0"
                label="Bruker"
                :disable="loading"
                :model-value="userFilter"
                :options="userOptions"
                option-label="fullName"
                option-value="id"
                emit-value
                map-options
                @filter="filterUsers"
                @update:model-value="
                  (val) => setFilter({ userId: val ?? null })
                "
              >
                <template #no-option>
                  <q-item>
                    <q-item-section class="text-grey">
                      Ingen treff
                    </q-item-section>
                  </q-item>
                </template>
              </q-select>
            </div>
          </div>
          <div class="row items-start q-gutter-sm q-pr-md">
            <div class="column">
              <q-radio
                dense
                :disable="loading"
                :model-value="approvedFilter"
                label="Ubehandlet"
                :val="3"
                @update:model-value="(val) => setFilter({ approved: val })"
                ><q-badge class="q-ml-xs" v-show="pendingHours > 0">{{
                  formatHours(pendingHours)
                }}</q-badge></q-radio
              >
              <div class="text-caption text-grey-7 q-pl-lg">
                Gjelder alle sesonger
              </div>
            </div>
            <q-radio
              dense
              :disable="loading"
              :model-value="approvedFilter"
              label="Godkjent"
              :val="1"
              @update:model-value="(val) => setFilter({ approved: val })"
              ><q-badge
                color="positive"
                class="q-ml-xs"
                v-show="approvedHours > 0"
                >{{ formatHours(approvedHours) }}</q-badge
              ></q-radio
            >
            <q-radio
              dense
              :disable="loading"
              :model-value="approvedFilter"
              label="Avslått"
              :val="2"
              @update:model-value="(val) => setFilter({ approved: val })"
              ><q-badge
                color="warning"
                class="q-ml-xs"
                v-show="rejectedHours > 0"
                >{{ formatHours(rejectedHours) }}</q-badge
              ></q-radio
            >
          </div>
          <q-space></q-space>
          <span class="q-pa-md row">
            <span class="q-pa-sm">
              <q-btn
                v-if="isAdmin"
                :disable="!(selectedWorkHours.length > 0)"
                label="Godkjenn"
                :size="$q.screen.gt.sm ? 'large' : 'medium'"
                @click="
                  showApprovalDialog = true;
                  approvalType = 1;
                "
                color="primary"
              />
            </span>
            <span class="q-pa-sm">
              <q-btn
                v-if="isAdmin"
                :disable="!(selectedWorkHours.length > 0)"
                label="Avslå"
                :size="$q.screen.gt.sm ? 'large' : 'medium'"
                @click="
                  showApprovalDialog = true;
                  approvalType = 2;
                "
                color="primary"
              />
            </span>
          </span>
          <q-card v-if="$q.screen.lt.md && approvedFilter === 3" class="col-12">
            <q-card-section>
              <q-checkbox
                v-model="selectAllBox"
                label="Velg alle"
                @update:model-value="toggleSelectAll"
              >
              </q-checkbox>
            </q-card-section>
          </q-card>
        </template>
        <template #item="props" v-if="$q.screen.lt.md">
          <q-card
            :props="props"
            class="q-pa-sm q-my-sm col-12 grid-style-transition cursor-pointer"
            :style="props.selected ? 'transform: scale(0.95);' : ''"
            v-ripple
            @click="openWorkHours(props.row)"
          >
            <q-card-section>
              <div class="row">
                <q-icon
                  size="lg"
                  name="check_circle"
                  class="green-text q-pr-lg"
                  v-if="props.row.approvalStatus === 1"
                />
                <q-icon
                  size="lg"
                  name="cancel"
                  class="red-text q-pr-lg"
                  v-if="props.row.approvalStatus === 2"
                />
                <!-- Valg for masse-godkjenning skal ikke åpne dialogen -->
                <div v-if="props.row.approvalStatus === null" @click.stop>
                  <q-checkbox v-model="props.selected" class="q-pr-md" />
                </div>
                <div class="text-h6 q-pr-lg">
                  <q-item-label caption>
                    <span>
                      <span> {{ toDateString(props.row.startTime) }} </span>
                      <span>
                        {{ toTimeString(props.row.startTime) }} -
                        {{ toTimeString(props.row.endTime) }}
                      </span>
                    </span>
                  </q-item-label>
                  {{ userNameById(props.row.userId) }}
                </div>
                <q-space></q-space>
                <q-item-label caption class="q-pt-md">
                  {{ formatHours(props.row.hours) }}
                </q-item-label>
              </div>
            </q-card-section>
            <q-separator></q-separator>
            <q-card-section>
              <q-item-label
                style="font-size: 14px"
                :caption="!props.row.description"
              >
                {{ props.row.description ?? "Ingen beskrivelse..." }}
              </q-item-label>
            </q-card-section>
            <q-separator></q-separator>
            <q-card-section class="row">
              <q-space></q-space>
              <q-item-label caption>
                {{ approvedByText(props.row) }}
              </q-item-label>
            </q-card-section>
          </q-card>
        </template>
        <template #header-selection="props" v-if="$q.screen.gt.md">
          <q-checkbox
            v-if="isAdmin && approvedFilter === 3"
            :props="props"
            v-model="props.selected"
          />
        </template>
        <template #body-selection="props">
          <q-checkbox
            v-if="!props.row.approvalStatus && isAdmin"
            :props="props"
            v-model="props.selected"
          />
        </template>
        <template #body-cell-status="props">
          <q-td :props="props">
            <q-icon
              size="md"
              name="check_circle"
              class="green-text"
              v-if="props.row.approvalStatus === 1"
            />
            <q-icon
              size="md"
              name="cancel"
              class="red-text"
              v-if="props.row.approvalStatus === 2"
            />
          </q-td>
        </template>
        <template #body-cell-from="props">
          <q-td :props="props">
            {{ toDateString(props.row.startTime) }}
            {{ toTimeString(props.row.startTime) }}
          </q-td>
        </template>
        <template #body-cell-to="props">
          <q-td :props="props">
            {{ toDateString(props.row.endTime) }}
            {{ toTimeString(props.row.endTime) }}
          </q-td>
        </template>
        <template #body-cell-hours="props">
          <q-td :props="props">
            {{ formatHours(props.row.hours) }}
          </q-td>
        </template>
      </q-table>
    </div>
  </q-page>
  <q-dialog v-model="showApprovalDialog">
    <q-card>
      <q-card-section class="text-h6">
        Bekreft {{ approvalType == 1 ? "godkjenning" : "avslag" }}
      </q-card-section>
      <q-card-section>
        <div>
          Er du sikker på at du vil sette {{ selectedWorkHours.length }}
          {{ selectedWorkHours.length > 1 ? "timeføringer" : "timeføring" }}
          til
          <span
            :class="
              approvalType == 1 ? 'green-text text-bold ' : 'red-text text-bold'
            "
            >{{ approvalType == 1 ? "godkjent" : "avslått" }}</span
          >?
        </div>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn
          v-if="isAdmin"
          :disable="!(selectedWorkHours.length > 0)"
          label="Avbryt"
          no-caps
          flat
          @click="showApprovalDialog = false"
        />
        <q-btn
          v-if="isAdmin"
          :disable="!(selectedWorkHours.length > 0)"
          label="Lagre"
          no-caps
          @click="approveUpdateRows(approvalType)"
          color="primary"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
  <q-dialog v-model="showEditDialog" persistent>
    <TimeTrackingForm
      :model-value="editWorkHour"
      allow-approval
      @cancel="showEditDialog = false"
      @saved="onWorkHourSaved"
    ></TimeTrackingForm>
  </q-dialog>
  <q-dialog v-model="showWorkHourDialog">
    <q-card class="q-pa-sm" style="width: 100%">
      <q-card-section>
        <div class="row">
          <q-btn-dropdown
            size="lg"
            :icon="dialogIcon.name"
            :class="dialogIcon.class"
            class="q-pa-xs q-pl-sm"
          >
            <q-list>
              <q-item
                v-if="foundWorkHour.approvalStatus !== null"
                clickable
                @click="changeStatus(foundWorkHour.workHourId, null)"
              >
                <q-item-section>
                  <div>
                    <q-icon
                      size="md"
                      class="q-pr-sm grey-text"
                      name="radio_button_unchecked"
                    />
                    Ingen status
                  </div>
                </q-item-section>
              </q-item>
            </q-list>
          </q-btn-dropdown>
          <div class="text-h6 q-px-lg q-pt-xs">
            <q-item-label caption>
              <span
                v-if="
                  toDateString(foundWorkHour.startTime) ==
                  toDateString(foundWorkHour.endTime)
                "
              >
                <span> {{ toDateString(foundWorkHour.startTime) }} | </span>
                <span>
                  {{ toTimeString(foundWorkHour.startTime) }} -
                  {{ toTimeString(foundWorkHour.endTime) }}
                </span>
              </span>
              <span
                v-if="
                  toDateString(foundWorkHour.startTime) !=
                  toDateString(foundWorkHour.endTime)
                "
              >
                <div>
                  Fra: {{ toDateString(foundWorkHour.startTime) }} |
                  {{ toTimeString(foundWorkHour.startTime) }}
                </div>
                <div>
                  Til: {{ toDateString(foundWorkHour.endTime) }} |
                  {{ toTimeString(foundWorkHour.endTime) }}
                </div>
              </span>
            </q-item-label>
            {{ userNameById(foundWorkHour.userId) }}
          </div>
          <q-space></q-space>
          <q-item-label caption class="q-pt-md">
            {{ formatHours(foundWorkHour.hours) }}
          </q-item-label>
        </div>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-section>
        <q-item-label :caption="!foundWorkHour.description">
          {{ foundWorkHour.description || "Ingen beskrivelse..." }}
        </q-item-label>
        <q-item-label v-if="foundWorkHour.modifiedBy" caption class="q-pt-sm">
          Endret av {{ foundWorkHour.modifiedByName ?? "ukjent" }}
          {{ toDateTimeString(foundWorkHour.modifiedTime) }}
        </q-item-label>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-actions>
        <q-space></q-space>
        <q-btn label="Lukk" @click="closeWorkHourDialog" />
      </q-card-actions>
      <q-card-section v-if="foundWorkHour.approvalStatus !== null" class="row">
        <q-space></q-space>
        <q-item-label caption>
          {{ approvedByText(foundWorkHour) }}
        </q-item-label>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>
<script setup lang="ts">
import { useQuasar } from "quasar";
import type { QTable, QTableColumn, QTableProps } from "quasar";
import { onMounted, ref, computed, useTemplateRef, nextTick } from "vue";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useUserStore } from "src/stores/UserStore";
import { useAuthStore } from "src/stores/AuthStore";
import { useSeasonStore } from "src/stores/SeasonStore";
import { format } from "date-fns";
import { useRoute, useRouter } from "vue-router";
import { formatHours, formatNumber } from "src/shared/formatter";
import {
  getWorkHourErrorKind,
  summarizeBulkApproval,
} from "src/shared/workHourDiff";
import { getApiErrorMessage } from "src/shared/apiError";
import type { BulkApprovalCounts } from "src/shared/workHourDiff";
import type { UserResponse, WorkHourResponse } from "src/types";
import TimeTrackingForm from "components/TimeTrackingForm.vue";

// Filteret som sendes til q-table (`:filter`) og tilbake i @request.
interface WorkHourFilter {
  approved: number;
  season: number | null;
  userId: number | null;
}

type TableRequestProps = Parameters<NonNullable<QTableProps["onRequest"]>>[0];

// store init
const $router = useRouter();
const $route = useRoute();
const workHourStore = useWorkHourStore();
const userStore = useUserStore();
const authStore = useAuthStore();
const seasonStore = useSeasonStore();
const $q = useQuasar();

// props and emits
const emit = defineEmits<{
  "toggle-right": [];
  "toggle-left": [];
}>();

// refs
const selectAllBox = ref(false);
const approvedHours = ref(0);
const pendingHours = ref(0);
const rejectedHours = ref(0);
// Starter tom; settes til raden som åpnes i dialogen.
const foundWorkHour = ref<Partial<WorkHourResponse>>({});
const showWorkHourDialog = ref(false);
const editWorkHour = ref<WorkHourResponse | null>(null);
const showEditDialog = ref(false);
const selectedWorkHours = ref<WorkHourResponse[]>([]);
const tableRef = useTemplateRef<QTable>("tableRef");
const loading = ref(false);
const seasonsLoaded = ref(false);
const userOptions = ref<UserResponse[]>([]);
const showApprovalDialog = ref(false);
// 1 = godkjenn, 2 = avslå. Settes alltid før bekreftelsesdialogen åpnes.
const approvalType = ref<1 | 2>(1);
const userWorkHours = ref<WorkHourResponse[]>([]);
const currentPage = ref(1);
const currentUser = computed(() => authStore.user);
// String(...) gir samme tolkning som parseInt på rå query-verdi (null/array).
// rowsNumber er utelatt (= undefined) til totalen er hentet; q-table krever
// at feltet er fraværende fremfor eksplisitt undefined (exactOptionalPropertyTypes).
const pagination = ref<{
  rowsPerPage: number;
  page: number;
  rowsNumber?: number;
}>({
  rowsPerPage: Number.isInteger(parseInt(String($route.query.rowPP)))
    ? parseInt(String($route.query.rowPP))
    : 15,
  page: Number.isInteger(parseInt(String($route.query.page)))
    ? parseInt(String($route.query.page))
    : 1,
});
const dialogIcon = ref({
  class: "",
  name: "",
});

// constants
const columns: QTableColumn<WorkHourResponse>[] = [
  {
    name: "status",
    label: "Status",
    field: (row) => row.approvalStatus,
    align: "left",
    headerStyle: "width: 10%",
    style: "width: 10%",
  },
  {
    name: "user",
    label: "Bruker",
    field: (row) => row.userId,
    format: (val: number) => userNameById(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "approvedBy",
    label: "Godkjent av",
    field: (row) => row.approvedByName ?? "",
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "description",
    label: "Beskrivelse",
    field: (row) => row.description,
    align: "left",
    headerStyle: "width: 50%",
    style:
      "width: 50%; max-width: 100px; text-overflow: ellipsis; overflow: hidden;",
  },
  {
    name: "from",
    label: "Fra",
    // `field` er påkrevd i QTableColumn; cellen rendres uansett via #body-cell-from.
    field: "startTime",
    format: (val: string | null | undefined) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "to",
    label: "Til",
    // `field` er påkrevd i QTableColumn; cellen rendres uansett via #body-cell-to.
    field: "endTime",
    format: (val: string | null | undefined) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "hours",
    label: "Timer",
    field: (row) => row.hours,
    format: (val: number | null | undefined) => formatNumber(val),
    align: "right",
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
];

// computed
const visibleColumns = computed<string[]>(() => {
  const cols: string[] = [];
  cols.push("user");
  if ($q.screen.gt.xs && approvedFilter.value !== 3) cols.push("status");
  if ($q.screen.gt.xs && approvedFilter.value !== 3) cols.push("approvedBy");
  if ($q.screen.gt.sm) cols.push("description");
  cols.push("from");
  cols.push("to");
  if ($q.screen.gt.sm) cols.push("hours");
  return cols;
});

const isAdmin = computed(() => currentUser.value?.isAdmin ?? false);

const approvedFilter = computed<number>(() => {
  return $route.query.a !== undefined ? parseInt(String($route.query.a)) : 3;
});

// Sesongens startår fra URL (`s`), ellers inneværende sesong.
const seasonFilter = computed<number | null>(() => {
  const fromQuery = parseInt(String($route.query.s));
  return Number.isInteger(fromQuery)
    ? fromQuery
    : seasonStore.currentSeason?.startYear ?? null;
});

// Valgt bruker fra URL (`u`), null = alle brukere.
const userFilter = computed<number | null>(() => {
  const fromQuery = parseInt(String($route.query.u));
  return Number.isInteger(fromQuery) ? fromQuery : null;
});

const filter = computed<WorkHourFilter>(() => {
  return {
    approved: approvedFilter.value,
    season: seasonFilter.value,
    userId: userFilter.value,
  };
});

// methods
function resetTable() {
  userWorkHours.value = [];
  currentPage.value = 1;
  delete pagination.value.rowsNumber;
}

async function getUserWorkHours(props: TableRequestProps) {
  // q-table sender tilbake objektet fra `:filter` (typet som any av Quasar).
  const filter = props.filter as WorkHourFilter;
  loading.value = true;
  resetTable();
  try {
    // Ubehandlede timer vises på tvers av sesonger, så de ikke skjules av
    // sesongfilteret. Axios utelater null/undefined params.
    const ignoreSeason = filter.approved === 3;
    const params = {
      approved: filter.approved,
      page: props.pagination.page,
      pageSize: props.pagination.rowsPerPage,
      season: ignoreSeason ? null : filter.season,
      userId: filter.userId,
    };

    const [response, seasonSums, allSeasonSums] = await Promise.all([
      workHourStore.getWorkHours(params),
      workHourStore.getWorkHoursSums(filter.userId, filter.season),
      workHourStore.getWorkHoursSums(filter.userId, null),
    ]);

    userWorkHours.value = response.result;
    // Samme objekt som storen nettopp satte i userWorkHours.
    pagination.value.rowsNumber = response.totalCount;
    pagination.value.page = props.pagination.page;
    pagination.value.rowsPerPage = props.pagination.rowsPerPage;

    // Godkjent/Avslått gjelder valgt sesong; Ubehandlet alle sesonger.
    approvedHours.value = seasonSums.approvedHours;
    rejectedHours.value = seasonSums.rejectedHours;
    pendingHours.value = allSeasonSums.pendingHours;
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      message: "Klarte ikke å hente timeføringer",
    });
  } finally {
    // Synker side/rader til URL-en (setFilter leser dem fra pagination).
    setFilter();
    loading.value = false;
  }
}

function closeWorkHourDialog() {
  showWorkHourDialog.value = false;
}

function onWorkHourSaved() {
  showEditDialog.value = false;
  tableRef.value?.requestServerInteraction();
}

async function approveUpdateRows(status: number) {
  const counts: Required<BulkApprovalCounts> = {
    ok: 0,
    alreadyProcessed: 0,
    notFound: 0,
    failed: 0,
  };
  try {
    loading.value = true;
    for (const workHour of selectedWorkHours.value) {
      try {
        await workHourStore.updateApproval({
          workHourId: workHour.workHourId,
          approvalStatus: status,
        });
        counts.ok++;
      } catch (e) {
        console.error(e);
        const kind = getWorkHourErrorKind(e);
        if (kind === "conflict") {
          counts.alreadyProcessed++;
        } else if (kind === "notFound") {
          counts.notFound++;
        } else {
          counts.failed++;
        }
      }
    }
    const summary = summarizeBulkApproval(counts, status);
    $q.notify({
      type: summary.type,
      message: summary.message,
    });
  } finally {
    loading.value = false;
    selectedWorkHours.value = [];
    showApprovalDialog.value = false;
    tableRef.value?.requestServerInteraction();
  }
}

async function changeStatus(
  workHourId: number | undefined,
  status: number | null
) {
  try {
    loading.value = true;
    // Dialogen åpnes kun for en funnet føring, så id er alltid satt her.
    if (workHourId === undefined) return;
    await workHourStore.updateApproval({
      workHourId: workHourId,
      approvalStatus: status,
    });
    $q.notify({
      message: "Status oppdatert",
      color: "positive",
    });
  } catch (e) {
    console.error(e);
    const kind = getWorkHourErrorKind(e);
    $q.notify({
      type: "negative",
      message: getApiErrorMessage(
        e,
        kind === "conflict"
          ? "Statusen kunne ikke endres fordi føringen er endret av noen andre"
          : kind === "notFound"
          ? "Føringen finnes ikke lenger"
          : kind === "forbidden"
          ? "Du har ikke tilgang til å endre denne føringen"
          : "Klarte ikke å oppdatere status"
      ),
    });
  } finally {
    loading.value = false;
    showWorkHourDialog.value = false;
    tableRef.value?.requestServerInteraction();
  }
}

async function openWorkHours(workHourRow: WorkHourResponse) {
  const found = userWorkHours.value?.find(
    (w) => w.workHourId === workHourRow.workHourId
  );
  if (!found) {
    return;
  }
  foundWorkHour.value = found;
  if (found.approvalStatus === null) {
    // Åpen føring: kan redigeres og godkjennes/avslås i skjemaet.
    editWorkHour.value = { ...found };
    showEditDialog.value = true;
    return;
  }
  if (found.approvalStatus === 1) {
    dialogIcon.value.class = "green-text";
    dialogIcon.value.name = "check_circle";
  } else if (found.approvalStatus === 2) {
    dialogIcon.value.class = "red-text";
    dialogIcon.value.name = "cancel";
  } else {
    dialogIcon.value.class = "grey-text";
    dialogIcon.value.name = "radio_button_unchecked";
  }
  showWorkHourDialog.value = true;
}

function toggleSelectAll(val: boolean) {
  if (val) {
    selectedWorkHours.value = [...userWorkHours.value];
  } else {
    selectedWorkHours.value = [];
  }
}

// Oppdaterer URL-query (a, page, rowPP, s, u). Felter som ikke er med i
// `changes` (approved, season, userId) beholder nåværende verdi; page og rowPP
// leses fra `pagination`.
async function setFilter(changes: Partial<WorkHourFilter> = {}) {
  const approved = changes.approved ?? approvedFilter.value;
  const season = "season" in changes ? changes.season : seasonFilter.value;
  const userId = "userId" in changes ? changes.userId : userFilter.value;

  if (approved !== 3) {
    selectedWorkHours.value = [];
  }
  if (
    approved !== approvedFilter.value ||
    season !== seasonFilter.value ||
    userId !== userFilter.value
  ) {
    pagination.value.page = 1;
    selectedWorkHours.value = [];
  }

  await $router.push({
    query: {
      a: approved || undefined,
      page: pagination.value.page || 1,
      rowPP: pagination.value.rowsPerPage || undefined,
      s: season ?? undefined,
      u: userId ?? undefined,
    },
  });
}

function filterUsers(val: string, update: (callbackFn: () => void) => void) {
  update(() => {
    const needle = (val ?? "").toLowerCase();
    userOptions.value = needle
      ? userStore.users.filter((u) =>
          (u.fullName ?? "").toLowerCase().includes(needle)
        )
      : userStore.users;
  });
}

type DateValue = string | Date | null | undefined;

function toDateTimeString(
  value: DateValue,
  options?: { includeSeconds?: boolean }
) {
  let timeFormat = "dd.MM.yyyy HH:mm";
  if (options && options.includeSeconds) timeFormat = "dd.MM.yyyy HH:mm:ss";
  return value ? format(ensureIsDate(value), timeFormat) : "";
}
function ensureIsDate(value: string | Date) {
  return value instanceof Date ? value : new Date(value);
}
function toTimeString(value: DateValue) {
  return value ? format(ensureIsDate(value), "HH:mm") : "";
}
function toDateString(value: DateValue) {
  return value ? format(ensureIsDate(value), "dd.MM.yyyy") : "";
}

function approvedByText(row: Partial<WorkHourResponse>) {
  if (!row.approvedBy || row.approvalStatus === null) return "";
  const name = row.approvedByName ?? "ukjent";
  return row.approvalStatus === 1
    ? `Godkjent av: ${name}`
    : `Avslått av: ${name}`;
}

function userNameById(id: number | undefined) {
  const approvedByNameUser = userStore.users.find((u) => u.id === id);
  return approvedByNameUser?.fullName ? approvedByNameUser?.fullName : "";
}

onMounted(async () => {
  try {
    await Promise.all([
      userStore.getUsers(),
      userStore.getUser(),
      seasonStore.getSeasons(),
    ]);
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      message: "Klarte ikke å hente sesonger eller brukere",
    });
  }
  userOptions.value = userStore.users;
  // Tabellen rendres først når sesong er kjent, så første forespørsel har riktig filter.
  seasonsLoaded.value = true;
  await nextTick();
  tableRef.value?.requestServerInteraction();
});
</script>
<style lang="scss" scoped>
.red-text {
  color: $red-4;
}
.green-text {
  color: $green-4;
}
.orange-text {
  color: $orange-4;
}
.grey-text {
  color: $grey-7;
}
.selection_width {
  width: 5%;
}
.grid-style-transition {
  transition: transform 0.28s, background-color 0.28s;
}
</style>
