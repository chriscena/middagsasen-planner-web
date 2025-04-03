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
        :disable="showEdit"
      />
      <q-btn
        v-if="timerStarted && !showEdit"
        class="col-12"
        size="lg"
        label="Avslutt"
        @click="stopTimer()"
        :disable="showEdit"
      />
    </div>
    <div class="col" v-if="showEdit">
      <div>
        <DatePickerInput
          class="q-pa-sm"
          dense
          filled
          label="Dato"
          v-model="hourLog.date"
        />
        <TimePickerInput
          class="q-pa-sm"
          dense
          filled
          label="Starttid"
          v-model="hourLog.startTime"
        />
        <TimePickerInput
          class="q-pa-sm"
          dense
          filled
          label="Sluttid"
          v-model="hourLog.endTime"
        />
      </div>
      <div class="row">
        <q-input
          class="col-12 q-pa-sm"
          filled
          type="textarea"
          label="Kommentar"
          v-model="hourLog.comment"
        />
        <div class="row q-pa-sm">
          <q-btn label="Fortsett" @click="continueTimer()" />
        </div>
        <q-space></q-space>
        <div class="row q-pa-sm">
          <q-btn color="primary" label="Lagre" @click="saveStoppedTimer()" />
        </div>
      </div>
    </div>
    <div>
      <q-table
        ref="tableRef"
        :rows="userWorkHours"
        :columns="columns"
        @request="checkWorkHoursNotEnded"
      />
    </div>
  </q-page>
</template>

<script setup>
import DatePickerInput from "src/components/DatePickerInput.vue";
import TimePickerInput from "src/components/TimePickerInput.vue";

import { useQuasar } from "quasar";
import { onMounted, ref, computed, useTemplateRef } from "vue";
import { date, Loading } from "quasar";
import { useWorkHourStore } from "src/stores/WorkHourStore";
import { useUserStore } from "src/stores/UserStore";
import { useAuthStore } from "src/stores/AuthStore";
import { parseISO, format } from "date-fns";

// store init
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
const currentUser = computed(() => authStore.user);
const userId = currentUser.value.id;

const hourLog = ref({
  startTime: null,
  endTime: null,
  comment: null,
});

// constants
const columns = [
  {
    name: "workHourId",
    label: "workHourId",
    field: (row) => row.workHourId,
    align: "left",
    sortable: true,
    headerStyle: "width: 5%",
    style: "width: 5%",
  },
  {
    name: "startTime",
    label: "startTime",
    field: (row) => row.startTime,
    align: "left",
    sortable: true,
    headerStyle: "width: 25%",
    style: "width: 25%",
  },
  {
    name: "UserId",
    label: "UserId",
    field: (row) => row.userId,
    align: "left",
    sortable: true,
    headerStyle: "width: 25%",
    style: "width: 25%",
  },
];

// methods
async function checkWorkHoursNotEnded() {
  await workHourStore.getWorkHoursByUser(userId);
  userWorkHours.value = workHourStore.userWorkHours;
  if (userWorkHours.value.length > 0) {
    if (endTimeHandler.value) {
      timerStarted.value = true;
      showEdit.value = false;
      await workHourStore.getWorkHoursById(endTimeHandler.value);
      currentWorkHour = workHourStore.workHourById;
    }
  }
}

const endTimeHandler = computed(() => {
  const noEndTime = userWorkHours.value.find(
    (workHour) => workHour.endTime == null
  );
  return noEndTime ? noEndTime.workHourId : null;
});

async function startTimer() {
  loading.value = true;
  // post startTime val
  try {
    const model = {
      userId: currentUser.value.id,
      startTime: new Date(),
    };
    const res = await workHourStore.createWorkHour(model);
    $q.notify({ message: "Vaktlista er lagt til." });
  } catch {
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

function continueTimer() {
  timerStarted.value = true;
  showEdit.value = false;
  // save val
}

function stopTimer() {
  timerStarted.value = false;

  //api call checkout endTime

  editStoppedTimer();
}

function editStoppedTimer() {
  // get and set values
  showEdit.value = true;
}

function saveStoppedTimer() {
  Loading.value = true;
  try {
    //save whole log
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
  }
}
onMounted(async () => {
  await userStore.getUser();
  tableRef.value?.requestServerInteraction();
});
</script>
