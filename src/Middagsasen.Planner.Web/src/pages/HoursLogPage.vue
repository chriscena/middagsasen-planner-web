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
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
          title="Din brukerinfo"
        ></q-btn>
      </q-toolbar>
    </q-header>
    <div class="q-pa-sm row">
      <q-btn
        v-if="!timerStarted && !showEdit"
        class="col-12"
        size="lg"
        color="primary"
        label="start"
        @click="startTimer()"
        :disable="showEdit || loading"
      />
      <q-btn
        v-if="timerStarted && !showEdit"
        class="col-12"
        size="lg"
        label="Rediger"
        @click="stopTimer()"
        :disable="showEdit"
      />
    </div>
    <div class="col" v-if="showEdit">
      <div>
        <DateTimePicker
          class="q-pa-sm"
          dense
          filled
          label="Starttid"
          v-model="workHour.startTime"
        />
        <DateTimePicker
          class="q-pa-sm"
          dense
          filled
          label="Sluttid"
          v-model="workHour.endTime"
        />
      </div>
      <div class="row">
        <q-input
          class="col-12 q-pa-sm"
          filled
          type="textarea"
          label="Kommentar"
          v-model="workHour.description"
        />
        <div class="row q-pa-sm">
          <q-btn label="Fortsett" @click="continueTimer()" />
        </div>
        <q-space></q-space>
        <div class="row q-pa-sm">
          <q-btn
            color="primary"
            label="Stopp og lagre"
            @click="saveStoppedTimer()"
          />
        </div>
      </div>
    </div>
    <div>
      <q-table
        flat
        ref="tableRef"
        :columns="columns"
        :loading="loading"
        :rows="userWorkHours"
        style="max-height: 85vh"
        no-data-label="Ingen data"
        class="sticky-header-table"
        v-model:pagination="pagination"
        @request="checkWorkHoursNotEnded"
      >
        <template #body-cell-fromTo="props">
          <q-td :props="props">
            <span v-if="props.row.startTime">
              {{ toTimeString(props.row.startTime) }}
            </span>
            <br />
            <span v-if="props.row.endTime">
              {{ toTimeString(props.row.endTime) }}
            </span>
          </q-td>
        </template>
        <template #body-cell-dates="props">
          <q-td :props="props">
            <span v-if="props.row.startTime">
              {{ toDateString(props.row.startTime) }}
            </span>
            <br />
            <span
              v-if="
                props.row.endTime &&
                toDateString(props.row.startTime) !=
                  toDateString(props.row.endTime)
              "
            >
              {{ toDateString(props.row.endTime) }}
            </span>
          </q-td>
        </template>
      </q-table>
    </div>
  </q-page>
</template>

<script setup>
import { useQuasar } from "quasar";
import { onMounted, ref, computed, useTemplateRef } from "vue";
import { Loading } from "quasar";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useUserStore } from "src/stores/UserStore";
import { useAuthStore } from "src/stores/AuthStore";
import { format, formatISO } from "date-fns";
import DateTimePicker from "src/components/DateTimePicker.vue";
import { useRoute, useRouter } from "vue-router";

// store init
const $router = useRouter();
const $route = useRoute();
const workHourStore = useWorkHourStore();
const userStore = useUserStore();
const authStore = useAuthStore();
const $q = useQuasar();

// props and emits
const emit = defineEmits(["toggle-right", "toggle-left"]);

// refs
const tableRef = useTemplateRef("tableRef");
const timerStarted = ref(false);
const showEdit = ref(false);
const loading = ref(false);
const userWorkHours = ref([]);
const currentWorkHour = ref({});
const currentPage = ref(1);
const currentUser = computed(() => authStore.user);
const userId = currentUser.value.id;
const activeWorkHour = ref({});
const workHour = ref({
  startTime: null,
  endTime: null,
  description: null,
});
const pagination = ref({
  rowsPerPage: Number.isInteger(parseInt($route.query.rowPP))
    ? parseInt($route.query.rowPP)
    : 20,
  page: Number.isInteger(parseInt($route.query.page))
    ? parseInt($route.query.page)
    : 1,
  rowsNumber: undefined,
});

// constants
const columns = [
  {
    name: "workHourId",
    label: "Nr.",
    field: (row) => row.workHourId,
    align: "left",
    sortable: true,
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
  {
    name: "fromTo",
    label: "Fra - Til",
    format: (val) => toDateTimeString(val),
    align: "left",
    sortable: true,
    headerStyle: "width: 10%",
    style: "width: 10%",
  },
  {
    name: "dates",
    label: "Dato/Datoer",
    format: (val) => toDateTimeString(val),
    align: "left",
    sortable: true,
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "hours",
    label: "Timer",
    field: (row) => row.hours,
    format: (val) => val.toFixed(2),
    align: "left",
    sortable: true,
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
];

// computed
const page = computed(() => {
  return $route.query.page;
});
const rowPP = computed(() => {
  return $route.query.rowPP;
});

// methods
function resetTable() {
  userWorkHours.value = [];
  currentPage.value = 1;
  pagination.value.rowsNumber = undefined;
}

async function checkWorkHoursNotEnded(props) {
  loading.value = true;
  try {
    resetTable();
    await workHourStore.getActiveWorkHour(userId);
    activeWorkHour.value = workHourStore.activeWorkHour;
    const params = {
      page: props.pagination.page,
      pageSize: props.pagination.rowsPerPage,
    };
    await workHourStore.getWorkHoursByUser(userId, params);
    userWorkHours.value = workHourStore.userWorkHours.result;
    pagination.value.rowsNumber = workHourStore.userWorkHours.totalCount;
    pagination.value.page = props.pagination.page;
    pagination.value.rowsPerPage = props.pagination.rowsPerPage;
    await $router.push({
      query: {
        page:
          pagination.value.page !== undefined || page.value !== undefined
            ? pagination.value.page ?? page.value
              ? pagination.value.page
              : 1
            : undefined,
        rowPP:
          pagination.value.rowsPerPage !== undefined ||
          rowPP.value !== undefined
            ? pagination.value.rowsPerPage ?? rowPP.value
              ? pagination.value.rowsPerPage
              : undefined
            : undefined,
      },
    });
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    if (userWorkHours.value.length > 0) {
      if (activeWorkHour.value) {
        timerStarted.value = true;
        showEdit.value = false;
        currentWorkHour.value = activeWorkHour.value;
      }
    }
    loading.value = false;
  }
}

async function startTimer() {
  loading.value = true;
  console.log(new Date());
  // post startTime val
  try {
    const model = {
      userId: currentUser.value?.id,
      startTime: formatISO(new Date()),
    };
    await workHourStore.createWorkHour(model);
    $q.notify({ message: "Timer startet." });
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    loading.value = false;
    timerStarted.value = true;
    tableRef.value?.requestServerInteraction();
  }
}

async function continueTimer() {
  loading.value = true;
  // save val
  try {
    const model = {
      workHourId: currentWorkHour.value.workHourId,
      startTime: workHour.value.startTime,
      endTime: null,
      description: workHour.value.description,
    };
    await workHourStore.updateWorkHour(model);
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: "errorOccurred",
    });
  } finally {
    loading.value = false;
    timerStarted.value = true;
    showEdit.value = false;
  }
}

async function stopTimer() {
  timerStarted.value = false;
  await editStoppedTimer();
}

async function editStoppedTimer() {
  // get and set values
  loading.value = true;
  try {
    await workHourStore.getWorkHourById(activeWorkHour.value.workHourId);
    currentWorkHour.value = workHourStore.workHourById;
    workHour.value.startTime = currentWorkHour.value.startTime;
    workHour.value.endTime = formatISO(new Date());
    workHour.value.description = currentWorkHour.value.description;
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: "errorOccurred",
    });
  } finally {
    loading.value = false;
    showEdit.value = true;
  }
}

async function saveStoppedTimer() {
  Loading.value = true;
  try {
    const model = {
      workHourId: currentWorkHour.value.workHourId,
      startTime: workHour.value.startTime,
      endTime: workHour.value.endTime,
      description: workHour.value.description,
      approvedBy: currentWorkHour.value.approvedBy,
      shiftId: currentWorkHour.value.shiftId,
      isDeleted: currentWorkHour.value.isDeleted,
      userId: currentWorkHour.value.userId,
    };
    await workHourStore.updateWorkHour(model);
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: "errorOccurred",
    });
  } finally {
    timerStarted.value = false;
    showEdit.value = false;
    loading.value = false;
    tableRef.value?.requestServerInteraction();
  }
}

function toDateTimeString(value, options) {
  let timeFormat = "dd.MM.yyyy HH:mm";
  if (options && options.includeSeconds) timeFormat = "dd.MM.yyyy HH:mm:ss";
  return value ? format(ensureIsDate(value), timeFormat) : "";
}
function ensureIsDate(value) {
  return value instanceof Date ? value : new Date(value);
}
function toTimeString(value) {
  return value ? format(ensureIsDate(value), "HH:mm") : "";
}
function toDateString(value) {
  return value ? format(ensureIsDate(value), "dd.MM.yyyy") : "";
}
onMounted(async () => {
  await userStore.getUser();
  tableRef.value?.requestServerInteraction();
});
</script>
