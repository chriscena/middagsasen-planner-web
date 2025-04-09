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
    <div class="q-pb-xl row">
      <q-btn
        v-if="!timerStarted && !showEdit"
        class="col-12"
        size="lg"
        color="primary"
        label="start"
        @click="startTimer()"
        :disable="showEdit || loading"
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
          :disable="loading"
        />
      </div>
      <div class="row">
        <q-input
          class="col-12 q-pa-sm"
          filled
          type="textarea"
          label="Kommentar"
          v-model="workHour.description"
          :disable="loading"
        />
        <div class="row q-pa-sm">
          <q-btn label="Lagre" @click="saveTimer()" :disable="loading" />
        </div>
        <q-space></q-space>
        <div class="row q-pa-sm">
          <q-btn
            color="primary"
            label="Stopp og lagre"
            @click="saveStoppedTimer()"
            :disable="loading"
          />
        </div>
      </div>
    </div>
    <div>
      <q-list role="list" separator>
        <!--<q-item clickable class="q-py-sm text-bold"
          ><q-item-section class="col-1">Godkjent</q-item-section>
          <q-item-section class="col-2">Bruker</q-item-section>
          <q-item-section class="col-2">Godkjent av</q-item-section>
          <q-item-section class="col-3"> Beskrivelse </q-item-section>
          <q-item-section class="col-1">Fra</q-item-section>
          <q-item-section class="col-1">-</q-item-section>
          <q-item-section class="col-1">Til</q-item-section>
          <q-item-section class="col-1">Timer</q-item-section>
        </q-item>-->
        <q-item
          v-for="workHour in userWorkHours"
          :key="workHour.workHourId"
          clickable
          class="q-py-sm row"
          ><q-item-section avatar>
            <q-icon
              size="md"
              name="check_circle"
              class="green-text"
              v-if="workHour.approvedBy != null"
            />
            <q-icon
              size="md"
              name="cancel"
              class="red-text"
              v-if="workHour.approvedBy == null"
            />
          </q-item-section>
          <q-item-section>
            <q-item-label caption
              ><span v-if="$q.screen.lt.md">
                {{ toDateString(workHour.startTime) }}
              </span>
              |
              <span>
                {{ toTimeString(workHour.startTime) }} -
                {{ toTimeString(workHour.endTime) }}
              </span>
            </q-item-label>
            <q-item-label :lines="2" class="q-pr-xl">
              {{ workHour.description }}
            </q-item-label>
          </q-item-section>
          <q-item-section v-if="$q.screen.gt.sm" class="col-1">
            <q-item-label>
              <span>
                {{ toDateString(workHour.startTime) }}
              </span>
              <span
                v-if="
                  toDateString(workHour.startTime) !==
                  toDateString(workHour.endTime)
                "
              >
                - {{ toDateString(workHour.endTime) }}
              </span>
            </q-item-label>
          </q-item-section>
          <q-item-section side>
            {{ workHour.hours.toFixed(1).toString().replace(".", ",") }} t
          </q-item-section>
        </q-item>
      </q-list>
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
    name: "approved",
    label: "Vedtatt",
    field: (row) => row.approvedBy,
    align: "left",
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
  {
    name: "description",
    label: "Beskrivelse",
    field: (row) => row.description,
    align: "left",
    headerStyle: "width: 30%",
    style:
      "width: 20%; max-width: 30px; text-overflow: ellipsis; overflow: hidden;",
  },
  {
    name: "fromTo",
    label: "Fra - Til",
    format: (val) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 10%",
    style: "width: 10%",
  },
  {
    name: "dates",
    label: "Dato/Datoer",
    format: (val) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
  {
    name: "hours",
    label: "Timer",
    field: (row) => row.hours,
    format: (val) => val.toFixed(2),
    align: "left",
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

const visibleColumns = computed(() => {
  let cols = [];
  cols.push("approved");
  if ($q.screen.gt.md) cols.push("description");
  cols.push("fromTo");
  cols.push("dates");
  cols.push("hours");
  return cols;
});

// methods
async function resetTable() {
  userWorkHours.value = [];
  currentPage.value = 1;
  pagination.value.rowsNumber = undefined;
}

async function checkWorkHoursNotEnded(props) {
  loading.value = true;
  try {
    await resetTable();
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
        showEdit.value = true;
        currentWorkHour.value = activeWorkHour.value;
        await setEditTimer();
      }
    }
    loading.value = false;
  }
}

async function startTimer() {
  loading.value = true;
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
    showEdit.value = true;
    tableRef.value?.requestServerInteraction();
  }
}

async function saveTimer() {
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
  }
}

async function setEditTimer() {
  // get and set values
  loading.value = true;
  try {
    await workHourStore.getWorkHourById(activeWorkHour.value.workHourId);
    currentWorkHour.value = workHourStore.workHourById;
    workHour.value.startTime = currentWorkHour.value.startTime;
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
      endTime: formatISO(new Date()),
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

function userNameById(id) {
  const approvedByNameUser = userStore.users.find((u) => u.id === id);
  return approvedByNameUser?.fullName ? approvedByNameUser?.fullName : "";
}
function userPhoneById(id) {
  const approvedByNameUser = userStore.users.find((u) => u.id === id);
  return approvedByNameUser?.phoneNo ? approvedByNameUser?.phoneNo : "";
}

onMounted(async () => {
  await userStore.getUsers();
  await userStore.getUser();
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
</style>
