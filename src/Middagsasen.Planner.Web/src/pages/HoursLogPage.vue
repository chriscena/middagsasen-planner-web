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
      <q-table
        v-if="!showEdit"
        flat
        row-key="workHourId"
        ref="tableRef"
        :columns="columns"
        :visible-columns="visibleColumns"
        :loading="loading"
        :rows="userWorkHours"
        no-data-label="Ingen data"
        class="sticky-header-table"
        style="height: 80vh"
        @virtual-scroll="getUserWorkhours"
        virtual-scroll
        :rows-per-page-options="[0]"
        :virtual-scroll-slice-size="10"
        :virtual-scroll-item-size="48"
        hide-bottom
        hide-header
        @row-click="(_, row) => editWorkHours(row)"
      >
        <template #top-left>
          <div class="row q-gutter-sm q-pr-md"></div>
        </template>
        <template #body-cell-approved="props">
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
        <template #body-cell-timeAndDescription="props">
          <q-td :props="props">
            <q-item-section>
              <q-item-label caption
                ><span v-if="$q.screen.lt.md"
                  ><span>
                    {{ toDateString(props.row.startTime) }}
                  </span>
                  <span
                    v-if="
                      toDateString(props.row.startTime) !=
                      toDateString(props.row.endTime)
                    "
                  >
                    <span> - {{ toDateString(props.row.endTime) }}</span>
                  </span>
                  <br
                    v-if="
                      toDateString(props.row.startTime) !=
                      toDateString(props.row.endTime)
                    "
                  />
                  <span
                    v-if="
                      $q.screen.lt.md &&
                      !(
                        toDateString(props.row.startTime) !=
                        toDateString(props.row.endTime)
                      )
                    "
                  >
                    |
                  </span>
                </span>
                <span>
                  {{ toTimeString(props.row.startTime) }} -
                  {{ toTimeString(props.row.endTime) }}
                </span>
              </q-item-label>
              <q-item-label
                :lines="2"
                class="q-pr-xl"
                style="
                  max-width: 60vw;
                  text-overflow: ellipsis;
                  overflow: hidden;
                "
              >
                {{ props.row.description }}
              </q-item-label>
            </q-item-section>
          </q-td>
        </template>
        <template #body-cell-dates="props">
          <q-td :props="props">
            <q-item-section v-if="$q.screen.gt.sm" class="col-1">
              <q-item-label>
                <span>
                  {{ toDateString(props.row.startTime) }}
                </span>
                <span
                  v-if="
                    toDateString(props.row.startTime) !==
                    toDateString(props.row.endTime)
                  "
                >
                  - {{ toDateString(props.row.endTime) }}
                </span>
              </q-item-label>
            </q-item-section>
          </q-td>
        </template>
        <template #body-cell-hours="props">
          <td :props="props">
            <q-item-section side>
              {{ props.row.hours?.toFixed(1).toString().replace(".", ",") }} t
            </q-item-section>
          </td>
        </template>
      </q-table>
    </div>
    <q-dialog v-model="showWorkHourDialog" maximized>
      <q-card class="q-pa-sm">
        <q-card-section>
          <div class="row">
            <q-icon
              size="lg"
              :name="dialogIcon.name"
              :class="dialogIcon.class"
              class="q-pr-md"
            />
            <div class="text-h6">Rediger</div>

            <q-space></q-space>

            <q-icon
              @click="confirmDeleteDialog = true"
              size="md"
              name="delete"
              class="q-pr-md cursor-pointer"
              :class="hoverClass"
              @mouseover="hoverClass = 'text-red'"
              @mouseleave="hoverClass = ''"
            />
          </div>

          <div class="col q-pa-md">
            <DateTimePicker
              class="q-pa-sm col-6"
              dense
              filled
              label="Starttid"
              v-model="workHour.startTime"
              :disable="loading"
            />
            <DateTimePicker
              class="q-pa-sm col-6"
              dense
              filled
              label="Sluttid"
              v-model="workHour.endTime"
              :disable="loading"
            />
          </div>
          <div class="row q-pa-md">
            <q-input
              class="col-12 q-pa-sm"
              filled
              type="textarea"
              label="Kommentar"
              v-model="workHour.description"
              :disable="loading"
            />
          </div>
        </q-card-section>
        <q-card-section class="q-px-xl row justify-center">
          <q-btn
            label="Avbryt"
            size="large"
            @click="showWorkHourDialog = false"
          />
          <q-space></q-space>
          <q-btn
            label="Lagre"
            size="large"
            @click="saveWorkHourForm"
            color="primary"
          />
        </q-card-section>
      </q-card>
    </q-dialog>
    <q-dialog
      v-model="confirmDeleteDialog"
      transition-show="scale"
      transition-hide="scale"
    >
      <q-card style="max-width: 300px">
        <q-card-section>
          <div class="text-h6">Bekreft sletting</div>
        </q-card-section>

        <q-card-section class="q-pt-none">
          Vil du slette denne jobbtimen?
        </q-card-section>

        <q-card-actions>
          <q-btn flat label="Avbryt" v-close-popup no-caps :disable="loading" />
          <q-space />
          <q-btn
            flat
            no-caps
            label="Slett"
            @click="deleteWorkHour(foundWorkHour.workHourId)"
            :disable="loading"
            :class="hoverClass2"
            @mouseover="hoverClass2 = 'text-red'"
            @mouseleave="hoverClass2 = ''"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
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
const hoverClass = ref(null);
const hoverClass2 = ref(null);
const confirmDeleteDialog = ref(false);
const showWorkHourDialog = ref(false);
const foundWorkHour = ref({});
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
  rowsPerPage: 10,
  rowsNumber: undefined,
});
const dialogIcon = ref({
  class: "",
  name: "",
});

// constants
const columns = [
  {
    name: "approved",
    label: "Status",
    field: (row) => row.approvedBy,
    align: "left",
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
  {
    name: "timeAndDescription",
    label: "Beskrivelse",
    field: (row) => row.description,
    align: "left",
    headerStyle: "width: 80%",
    style:
      "width: 80%; max-width: 6000px; text-overflow: ellipsis; overflow: hidden;",
  },
  {
    name: "time",
    label: "Fra - Til",
    format: (val) => toDateTimeString(val),
    align: "left",
    headerStyle: "width: 15%",
    style: "width: 15%",
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
    headerStyle: "width: 15%",
    style: "width: 15%",
  },
];

// computed

const visibleColumns = computed(() => {
  let cols = [];
  cols.push("approved");
  cols.push("timeAndDescription");
  if ($q.screen.gt.sm) cols.push("dates");
  cols.push("hours");
  return cols;
});

// methods
async function resetTable() {
  userWorkHours.value = [];
  currentPage.value = 1;
  pagination.value.rowsNumber = undefined;
}

async function getUserWorkhours({ to, ref }) {
  if (
    to < userWorkHours.value.length - 1 ||
    userWorkHours.value.length >= pagination.value.rowsNumber
  )
    return;
  try {
    loading.value = true;
    const params = {
      size: pagination.value.rowsPerPage,
    };
    await workHourStore.getWorkHoursByUser(userId, params);
    userWorkHours.value = workHourStore.userWorkHours.result;
    pagination.value.rowsNumber = userWorkHours.value.length;
    pagination.value.rowsPerPage = workHourStore.userWorkHours.totalCount;
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    loading.value = false;
    if (pagination.value.rowsNumber == undefined) {
      pagination.value.rowsNumber = 0;
      if (ref) ref.refresh();
      console.group("lol");
    }
  }
}

async function checkWorkHoursNotEnded() {
  try {
    loading.value = true;
    await resetTable();
    await workHourStore.getActiveWorkHour(userId);
    activeWorkHour.value = workHourStore.activeWorkHour;
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: ("errorOccurred", { error: e }),
    });
  } finally {
    if (activeWorkHour.value) {
      timerStarted.value = true;
      showEdit.value = true;
      currentWorkHour.value = activeWorkHour.value;
      await setEditTimer();
    }
    loading.value = false;
  }
}

async function startTimer() {
  loading.value = true;
  // post startTime val
  try {
    workHour.value.startTime = null;
    workHour.value.endTime = null;
    workHour.value.description = null;
    const model = {
      userId: currentUser.value?.id,
      startTime: formatISO(new Date()),
    };
    await workHourStore.createWorkHour(model);
    currentWorkHour.value = workHourStore.workHour;
    workHour.value.startTime = currentWorkHour.value.startTime;
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
      endTime: workHour.value.endTime ?? formatISO(new Date()),
      description: workHour.value.description,
      approvedBy: currentWorkHour.value.approvedBy,
      shiftId: currentWorkHour.value.shiftId,
      approvalStatus: currentWorkHour.value.approvalStatus,
      userId: currentWorkHour.value.userId,
    };
    await workHourStore.updateWorkHour(model);
    $q.notify({ message: "Lagret." });
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
    await resetTable();
    workHour.value.endTime = null;
    loading.value = false;
  }
}

async function editWorkHours(workHourRow) {
  foundWorkHour.value = userWorkHours.value.find(
    (w) => w.workHourId === workHourRow.workHourId
  );
  if (!foundWorkHour.value) {
    return;
  }
  if (foundWorkHour.value.approvalStatus === 1) {
    dialogIcon.value.class = "green-text";
    dialogIcon.value.name = "check_circle";
  } else if (foundWorkHour.value.approvalStatus === 2) {
    dialogIcon.value.class = "red-text";
    dialogIcon.value.name = "cancel";
  }

  showWorkHourDialog.value = true;
  workHour.value.startTime = foundWorkHour.value.startTime;
  workHour.value.endTime = foundWorkHour.value.endTime;
  workHour.value.description = foundWorkHour.value.description;
}

async function saveWorkHourForm() {
  try {
    loading.value = true;
    const model = {
      workHourId: foundWorkHour.value.workHourId,
      startTime: workHour.value.startTime,
      endTime: workHour.value.endTime,
      description: workHour.value.description,
      approvedBy: foundWorkHour.value.approvedBy,
      shiftId: foundWorkHour.value.shiftId,
      approvalStatus: foundWorkHour.value.approvalStatus,
      userId: foundWorkHour.value.userId,
    };
    await workHourStore.updateWorkHour(model);
    $q.notify({ message: "Lagret." });
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: "errorOccurred",
    });
  } finally {
    workHour.value.endTime = null;
    showWorkHourDialog.value = false;
    await resetTable();
    loading.value = false;
  }
}

async function deleteWorkHour(workHourId) {
  try {
    loading.value = true;
    await workHourStore.deleteWorkHourById(workHourId);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: "Slettet",
    });
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      closeBtn: "close",
      message: "errorOccurred",
    });
  } finally {
    showWorkHourDialog.value = false;
    confirmDeleteDialog.value = false;
    await resetTable();
    loading.value = false;
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
  await checkWorkHoursNotEnded();
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
</style>
