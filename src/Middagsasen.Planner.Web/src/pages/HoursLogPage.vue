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
        <q-toolbar-title>Timeføring</q-toolbar-title>
        <q-space></q-space>
        <q-btn
          title="Timeføring"
          dense
          flat
          round
          icon="more_time"
          @click="openTimetrackingForm"
        />
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
    <div class="q-pb-sm">
      <q-select
        dense
        outlined
        label="Sesong"
        :style="$q.screen.lt.md ? '' : 'max-width: 300px'"
        :disable="viewModel.loading || viewModel.season === null"
        :model-value="viewModel.season"
        :options="seasonStore.seasons"
        option-label="label"
        option-value="startYear"
        emit-value
        map-options
        @update:model-value="onSeasonChanged"
      />
    </div>
    <div>
      <q-list role="list" separator>
        <q-infinite-scroll
          v-if="viewModel.seasonsLoaded"
          :offset="100"
          @load="getUserWorkhours"
          :distance="100"
          ref="infiniteScroll"
        >
          <q-item dense v-if="viewModel.pendingHoursSum > 0">
            <q-item-section avatar>
              <q-icon :name="openDisplay.icon" :class="openDisplay.iconClass" />
            </q-item-section>

            <q-item-section>
              <q-item-label overline>Ubehandlede timer </q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-item-label overline>
                {{ formatNumber(viewModel.pendingHoursSum) }}
                t
              </q-item-label></q-item-section
            >
          </q-item>
          <q-separator v-if="viewModel.pendingHoursSum > 0" />
          <q-item dense v-if="viewModel.approvedHoursSum > 0">
            <q-item-section avatar>
              <q-icon
                :name="approvedDisplay.icon"
                :class="approvedDisplay.iconClass"
              />
            </q-item-section>

            <q-item-section>
              <q-item-label overline>Godkjente timer </q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-item-label overline>
                {{ formatNumber(viewModel.approvedHoursSum) }}
                t
              </q-item-label></q-item-section
            >
          </q-item>
          <q-separator v-if="viewModel.approvedHoursSum > 0" />
          <q-item dense v-if="viewModel.rejectedHoursSum > 0">
            <q-item-section avatar>
              <q-icon
                :name="rejectedDisplay.icon"
                :class="rejectedDisplay.iconClass"
              />
            </q-item-section>

            <q-item-section>
              <q-item-label overline>Avslåtte timer </q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-item-label overline>
                {{ formatNumber(viewModel.rejectedHoursSum) }}
                t
              </q-item-label></q-item-section
            >
          </q-item>
          <q-separator v-if="viewModel.rejectedHoursSum > 0" />
          <q-item v-if="viewModel.noResults">
            <q-item-section class="text-center text-grey-7"
              >Ingen timer funnet</q-item-section
            >
          </q-item>
          <q-item
            separator
            v-for="hours in viewModel.userWorkHours"
            :key="hours.workHourId"
            clickable
            v-ripple
            @click="editWorkHour(hours)"
          >
            <q-item-section avatar>
              <q-icon
                :name="getApprovalStatusDisplay(hours.approvalStatus).icon"
                :class="
                  getApprovalStatusDisplay(hours.approvalStatus).iconClass
                "
              />
            </q-item-section>

            <q-item-section>
              <q-item-label
                >{{ formatDate(hours.startTime) }}
                {{ formatTime(hours.startTime) }} -
                {{ formatTime(hours.endTime) }}
              </q-item-label>
              <q-item-label caption>{{ hours.description }}</q-item-label>
              <q-item-label caption v-if="hours.modifiedBy" class="text-italic">
                Endret av {{ hours.modifiedByName ?? "ukjent" }}
              </q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-item-label>
                {{ formatHours(hours.hours) }}
              </q-item-label></q-item-section
            >
          </q-item>
          <template #loading>
            <div class="row justify-center q-my-md">
              <q-spinner size="3em" color="primary"></q-spinner>
            </div>
          </template>
        </q-infinite-scroll>
      </q-list>
    </div>
    <q-dialog
      v-model="viewModel.showForm"
      persistent
      @hide="onTimeTrackingFormClosed"
    >
      <TimeTrackingForm
        :model-value="viewModel.selectedWorkHours"
        @cancel="viewModel.showForm = false"
        @saved="onWorkHourSaved"
      ></TimeTrackingForm>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { useQuasar } from "quasar";
import type { QInfiniteScroll } from "quasar";
import { computed, useTemplateRef, reactive, onMounted } from "vue";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useAuthStore } from "src/stores/AuthStore";
import { useSeasonStore } from "src/stores/SeasonStore";
import { useRouter } from "vue-router";
import TimeTrackingForm from "components/TimeTrackingForm.vue";
import { formatHours, formatNumber } from "src/shared/formatter";
import { formatDate, formatTime } from "src/shared/time";
import { getSeasonStartYear } from "src/shared/season";
import { getApprovalStatusDisplay } from "src/shared/workHours";
import { ApprovalStatus } from "src/types";
import type { WorkHourResponse } from "src/types";

// store init
const $router = useRouter();
const workHourStore = useWorkHourStore();
const authStore = useAuthStore();
const seasonStore = useSeasonStore();
const $q = useQuasar();

// props and emits
const emit = defineEmits<{
  "toggle-right": [];
  "toggle-left": [];
}>();

interface HoursLogViewModel {
  loading: boolean;
  userWorkHours: WorkHourResponse[];
  showForm: boolean;
  selectedWorkHours: WorkHourResponse | null;
  approvedHoursSum: number;
  pendingHoursSum: number;
  rejectedHoursSum: number;
  season: number | null;
  seasonsLoaded: boolean;
  noResults: boolean;
}

const viewModel = reactive<HoursLogViewModel>({
  loading: false,
  userWorkHours: [],
  showForm: false,
  selectedWorkHours: null,
  approvedHoursSum: 0,
  pendingHoursSum: 0,
  rejectedHoursSum: 0,
  // Sesongens startår; null til sesonger er lastet.
  season: null,
  seasonsLoaded: false,
  noResults: false,
});

const infiniteScroll = useTemplateRef<QInfiniteScroll>("infiniteScroll");

// Ikonene i summene per status.
const openDisplay = getApprovalStatusDisplay(null);
const approvedDisplay = getApprovalStatusDisplay(ApprovalStatus.Approved);
const rejectedDisplay = getApprovalStatusDisplay(ApprovalStatus.Rejected);

const currentUser = computed(() => authStore.user);
// Siden ligger bak innlogging (router-guard), så user er satt her.
const userId = currentUser.value!.id;

// Økes når listen tømmes, slik at svar fra kall startet før det forkastes.
let loadGeneration = 0;
let sumsGeneration = 0;

async function getUserWorkhours(index: number, done: (stop?: boolean) => void) {
  const generation = loadGeneration;
  let stop = false;
  try {
    viewModel.loading = true;
    // Axios utelater null/undefined params.
    const params = {
      page: index,
      pageSize: 20,
      season: viewModel.season,
    };
    const response = await workHourStore.getWorkHoursByUser(userId, params);
    // Utdatert svar: ikke rør listen. done(false) lar infinite scroll laste
    // første side på nytt for den nye listen.
    if (generation !== loadGeneration) return;
    if (response.result.length > 0) {
      viewModel.userWorkHours.push(...response.result);
    }
    stop =
      viewModel.userWorkHours.length >= response.totalCount ||
      response.result.length === 0;
    viewModel.noResults = stop && viewModel.userWorkHours.length === 0;
  } catch (e) {
    if (generation !== loadGeneration) return;
    console.error(e);
    stop = true;
    $q.notify({
      type: "negative",
      message: "Klarte ikke å hente timeføringer",
    });
  } finally {
    viewModel.loading = false;
    done(stop);
  }
}

async function getWorkHoursSums() {
  const generation = ++sumsGeneration;
  const response = await workHourStore.getWorkHoursSums(
    userId,
    viewModel.season
  );
  // Et nyere kall er startet; ignorer utdatert svar.
  if (generation !== sumsGeneration) return;
  viewModel.approvedHoursSum = response.approvedHours;
  viewModel.pendingHoursSum = response.pendingHours;
  viewModel.rejectedHoursSum = response.rejectedHours;
}

function editWorkHour(hours: WorkHourResponse) {
  viewModel.selectedWorkHours = { ...hours };
  viewModel.showForm = true;
}

// Tømmer listen, starter infinite scroll på nytt og henter summer.
async function reload() {
  loadGeneration++;
  viewModel.userWorkHours = [];
  viewModel.noResults = false;
  infiniteScroll.value?.reset();
  infiniteScroll.value?.resume();
  await getWorkHoursSums();
}

// Bytter til sesongen den lagrede føringen tilhører, slik at den er synlig
// når listen lastes på nytt (reload skjer når dialogen lukkes).
function onWorkHourSaved(savedWorkHour: WorkHourResponse | null) {
  const season = getSeasonStartYear(savedWorkHour?.startTime);
  if (
    season !== null &&
    season !== viewModel.season &&
    seasonStore.seasons.some((s) => s.startYear === season)
  ) {
    viewModel.season = season;
  }
  viewModel.showForm = false;
}

async function onTimeTrackingFormClosed() {
  viewModel.selectedWorkHours = null;
  await reload();
}

async function onSeasonChanged(season: number | null) {
  if (season === viewModel.season) return;
  viewModel.season = season;
  await reload();
}

onMounted(async () => {
  try {
    await seasonStore.getSeasons();
    viewModel.season = seasonStore.currentSeason?.startYear ?? null;
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      message: "Klarte ikke å hente sesonger",
    });
  }
  // Infinite scroll rendres først når sesong er satt, så første side filtreres riktig.
  viewModel.seasonsLoaded = true;
  await getWorkHoursSums();
});

function openTimetrackingForm() {
  viewModel.showForm = true;
}
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
</style>
