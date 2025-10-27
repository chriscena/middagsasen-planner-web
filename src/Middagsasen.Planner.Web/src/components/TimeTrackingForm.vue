<template>
  <q-card class="full-width">
    <q-card-section class="text-h6">Timeføring</q-card-section>
    <q-card-section class="q-gutter-sm">
      <DatePickerInput
        v-model="viewModel.startDate"
        @blur="setStartDate"
        label="Startdato"
        :disable="loading"
      />
      <TimePickerInput
        autofocus
        v-model="viewModel.startTime"
        @blur="setStartTime"
        label="Starttid"
        :disable="loading"
        :error="!viewModel.startTimeValid"
      />
      <TimePickerInput
        v-model="viewModel.endTime"
        @blur="setEndTime"
        label="Sluttid"
        :error="!viewModel.endTimeValid"
        :disable="loading"
        :hint="endDate"
      />
      <q-input
        outlined
        readonly
        label="Antall timer"
        :modelValue="calculatedHours"
        :disable="loading"
      />
      <q-input
        outlined
        type="textarea"
        label="Kommentar"
        v-model="viewModel.description"
        :disable="loading"
      />
    </q-card-section>
    <q-card-actions align="right">
      <q-btn
        no-caps
        flat
        label="Avbryt"
        @click="emit('cancel')"
        :disable="viewModel.saving"
      ></q-btn>
      <q-btn
        no-caps
        unelevated
        color="primary"
        label="Lagre"
        @click="saveHours"
        :loading="viewModel.saving"
      ></q-btn>
    </q-card-actions>
  </q-card>
</template>

<script setup>
import { computed, ref, reactive } from "vue";
import { addDays, parse, format, isValid } from "date-fns";
import { useWorkHourStore } from "stores/WorkHourStore";
import { useAuthStore } from "src/stores/AuthStore";
import TimePickerInput from "./TimePickerInput.vue";
import DatePickerInput from "./DatePickerInput.vue";
import { useQuasar } from "quasar";

const emit = defineEmits(["cancel", "saved"]);
const $q = useQuasar();
const workHourStore = useWorkHourStore();
const authStore = useAuthStore();
const user = computed(() => authStore.user);
const loading = ref(false);

const viewModel = reactive({
  startDateTime: null,
  endDateTime: null,
  startDate: format(new Date(), "dd.MM.yyyy"),
  startTime: format(new Date(), "HH:mm"),
  startTimeValid: true,
  endTime: format(new Date(), "HH:mm"),
  endTimeValid: true,
  description: null,
  saving: false,
  loading: false,
});

const startDate = computed(() => {
  return format(new Date(workHour.value.startTime), "dd.MM.yyyy");
});
function setStartDate() {
  calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
}
function setStartTime() {
  calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
}
function setEndTime() {
  calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
}

function calculateTime(startDate, startTime, endTime) {
  if (!startDate || !startTime || !endTime) return;
  let start, end;
  try {
    start = parse(`${startDate} ${startTime}`, "dd.MM.yyyy HH:mm", new Date());
    viewModel.startTimeValid = true;
    viewModel.startDateTime = start.toISOString();
  } catch (error) {
    viewModel.startDateTime = null;
    viewModel.startTimeValid = false;
    return;
  }
  try {
    end = parse(`${startDate} ${endTime}`, "dd.MM.yyyy HH:mm", new Date());
    if (end < start) {
      end = addDays(end, 1);
    }
    viewModel.endTimeValid = true;
    viewModel.endDateTime = end.toISOString();
  } catch (error) {
    viewModel.endDateTime = null;
    viewModel.endTimeValid = false;
  }
}

const endDate = computed(() => {
  if (!viewModel.endDateTime) return undefined;
  let endDate = format(new Date(viewModel.endDateTime), "dd.MM.yyyy");
  return endDate != viewModel.startDate ? `Sluttdato: ${endDate}` : undefined;
});

const calculatedHours = computed(() => {
  if (!viewModel.startDateTime || !viewModel.endDateTime) return null;
  const start = new Date(viewModel.startDateTime);
  const end = new Date(viewModel.endDateTime);
  const diff = (end - start) / (1000 * 60 * 60); // Convert milliseconds to hours
  const hours = Math.floor(diff);
  const minutes = Math.round((diff - hours) * 60);
  return `${hours}:${minutes.toString().padStart(2, "0")}`;
});

async function saveHours() {
  try {
    const payload = {
      startTime: viewModel.startDateTime,
      endTime: viewModel.endDateTime,
      description: viewModel.description,
      userId: user.value.id,
    };
    const result = await workHourStore.createWorkHour(payload);
    emit("saved", result);
    $q.notify({
      message: "Timer lagret",
      color: "positive",
    });
  } catch (error) {}
}
</script>
