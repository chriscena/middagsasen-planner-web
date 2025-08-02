<template>
  <q-card class="full-width">
    <q-card-section class="text-h6">Timeføring</q-card-section>
    <q-card-section class="q-gutter-sm">
      <DatePickerInput
        :model-value="startDate"
        @update:model-value="setStartDate"
        label="Startdato"
        :disable="loading"
      />
      <TimePickerInput
        :model-value="startTime"
        @update:model-value="setStartTime"
        label="Starttid"
        :disable="loading"
      />
      <TimePickerInput
        :model-value="endTime"
        @update:model-value="setEndTime"
        label="Sluttid"
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
        v-model="workHour.description"
        :disable="loading"
      />
    </q-card-section>
    <q-card-actions align="right">
      <q-btn no-caps flat label="Avbryt" @click="emit('cancel')"></q-btn>
      <q-btn
        no-caps
        unelevated
        color="primary"
        label="Lagre"
        @click="saveHours"
      ></q-btn>
    </q-card-actions>
  </q-card>
</template>

<script setup>
import { computed, ref } from "vue";
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
const workHour = ref({
  startTime: parse(
    format(new Date(), "dd.MM.yyyy HH:mm"),
    "dd.MM.yyyy HH:mm",
    new Date()
  ).toISOString(),
  endTime: null,
  description: null,
});
const startDate = computed(() => {
  return format(new Date(workHour.value.startTime), "dd.MM.yyyy");
});
function setStartDate(date) {
  workHour.value.startTime = date
    ? parse(
        `${date} ${startTime.value}`,
        "dd.MM.yyyy HH:mm",
        new Date()
      ).toISOString()
    : null;
}
const startTime = computed(() => {
  return format(new Date(workHour.value.startTime), "HH:mm");
});
function setStartTime(time) {
  workHour.value.startTime = time
    ? parse(
        `${startDate.value} ${time}`,
        "dd.MM.yyyy HH:mm",
        new Date()
      ).toISOString()
    : null;
}
const endTime = computed(() => {
  if (!workHour.value.endTime) return null;
  return format(new Date(workHour.value.endTime), "HH:mm");
});
function setEndTime(time) {
  let endTime = time
    ? parse(`${startDate.value} ${time}`, "dd.MM.yyyy HH:mm", new Date())
    : null;
  if (endTime && endTime < new Date(workHour.value.startTime)) {
    endTime = addDays(endTime, 1);
  }
  workHour.value.endTime = endTime?.toISOString();
}
const endDate = computed(() => {
  if (!workHour.value.endTime) return undefined;
  let endDate = format(new Date(workHour.value.endTime), "dd.MM.yyyy");
  return endDate != startDate.value ? `Sluttdato: ${endDate}` : undefined;
});

const calculatedHours = computed(() => {
  if (!workHour.value.startTime || !workHour.value.endTime) return null;
  const start = new Date(workHour.value.startTime);
  const end = new Date(workHour.value.endTime);
  const diff = (end - start) / (1000 * 60 * 60); // Convert milliseconds to hours
  const hours = Math.floor(diff);
  const minutes = Math.round((diff - hours) * 60);
  return `${hours}:${minutes.toString().padStart(2, "0")}`;
});

async function saveHours() {
  const payload = {
    startTime: workHour.value.startTime,
    endTime: workHour.value.endTime,
    description: workHour.value.description,
    userId: user.value.id,
  };
  const result = await workHourStore.createWorkHour(payload);
  emit("saved", result);
  $q.notify({
    message: "Timer lagret",
    color: "positive",
  });
}
</script>
