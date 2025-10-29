<template>
  <q-card class="full-width">
    <q-card-section class="text-h6"
      >Timeføring
      <q-badge v-if="viewModel.status === 1" color="positive">Godkjent</q-badge>
      <q-badge v-if="viewModel.status === 2" color="negative">Avvist</q-badge>
    </q-card-section>
    <q-card-section class="q-gutter-sm">
      <DatePickerInput
        v-model="viewModel.startDate"
        @blur="setStartDate"
        label="Startdato"
        :disable="loading"
        :readonly="!canSave"
        :error="!viewModel.startDateValid"
        required
      />
      <TimePickerInput
        autofocus
        v-model="viewModel.startTime"
        @blur="setStartTime"
        label="Starttid"
        :disable="loading"
        :error="!viewModel.startTimeValid"
        :readonly="!canSave"
      />
      <TimePickerInput
        v-model="viewModel.endTime"
        @blur="setEndTime"
        label="Sluttid"
        :error="!viewModel.endTimeValid"
        :disable="loading"
        :hint="endDate"
        :readonly="!canSave"
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
        :readonly="!canSave"
        @blur="validateDescription"
        :error="!viewModel.descriptionValid"
      />
    </q-card-section>
    <q-card-actions align="right">
      <q-btn
        v-if="canDelete"
        no-caps
        flat
        color="negative"
        label="Slett"
        @click="deleteHours"
        :disable="viewModel.saving"
        :loading="viewModel.deleting"
      ></q-btn>
      <q-space></q-space>
      <q-btn
        no-caps
        flat
        label="Avbryt"
        @click="emit('cancel')"
        :disable="viewModel.saving || viewModel.deleting"
      ></q-btn>
      <q-btn
        v-if="canSave"
        no-caps
        unelevated
        color="primary"
        label="Lagre"
        @click="saveHours"
        :disable="!validForm || viewModel.deleting"
        :loading="viewModel.saving"
      ></q-btn>
    </q-card-actions>
  </q-card>
</template>

<script setup>
import { computed, ref, reactive, onMounted } from "vue";
import { addDays, parse, format } from "date-fns";
import { useWorkHourStore } from "stores/WorkHourStore";
import { useAuthStore } from "src/stores/AuthStore";
import TimePickerInput from "./TimePickerInput.vue";
import DatePickerInput from "./DatePickerInput.vue";
import { useQuasar } from "quasar";

const props = defineProps({
  modelValue: {
    type: Object,
    default: undefined,
  },
});

const emit = defineEmits(["cancel", "saved"]);
const $q = useQuasar();
const workHourStore = useWorkHourStore();
const authStore = useAuthStore();
const user = computed(() => authStore.user);
const loading = ref(false);

const viewModel = reactive({
  id: null,
  startDateTime: null,
  endDateTime: null,
  startDate: format(new Date(), "dd.MM.yyyy"),
  startDateValid: true,
  startTime: format(new Date(), "HH:mm"),
  startTimeValid: true,
  endTime: format(new Date(), "HH:mm"),
  endTimeValid: true,
  description: null,
  descriptionValid: true,
  status: 0,
  saving: false,
  deleting: false,
  loading: false,
});

function setStartDate() {
  viewModel.startDateValid = viewModel.startDate;
  calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
}
function setStartTime() {
  viewModel.startTimeValid = viewModel.startTime;
  calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
}
function setEndTime() {
  viewModel.endTimeValid = viewModel.endTime;
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
  if (!viewModel.startTimeValid || !viewModel.endTimeValid) {
    $q.notify({
      message: "Vennligst sjekk at tidspunktene er gyldige",
      color: "negative",
    });
    return;
  }
  if (!calculatedHours.value || calculatedHours.value === "0:00") {
    $q.notify({
      message: "Null timer gidder vi ikke å lagre vel. 😝",
      color: "negative",
    });
    return;
  }

  if (props.modelValue) {
    await updateHours();
  } else {
    await createHours();
  }
}

async function createHours() {
  try {
    viewModel.saving = true;
    const payload = {
      startTime: viewModel.startDateTime,
      endTime: viewModel.endDateTime,
      description: viewModel.description,
      userId: user.value.id,
    };
    const result = await workHourStore.createWorkHour(payload);
    emit("saved", result);
    $q.notify({
      message: "Timer lagret, bra jobba! 🙌",
      color: "positive",
    });
  } catch (error) {
    $q.notify({
      message: "Klarte ikke å lagre timer",
      color: "negative",
    });
  } finally {
    viewModel.saving = false;
  }
}

const validForm = computed(() => {
  return (
    viewModel.startTimeValid &&
    viewModel.endTimeValid &&
    descriptionIsValid.value
  );
});

async function updateHours() {
  try {
    viewModel.saving = true;
    const payload = {
      workHourId: viewModel.id,
      startTime: viewModel.startDateTime,
      endTime: viewModel.endDateTime,
      description: viewModel.description,
      userId: user.value.id,
    };
    const result = await workHourStore.updateWorkHour(payload);
    emit("saved", result);
    $q.notify({
      message: "Endringer lagret",
      color: "positive",
    });
  } catch (error) {
    $q.notify({
      message: "Klarte ikke å lagre endringer",
      color: "negative",
    });
  } finally {
    viewModel.saving = false;
  }
}

async function deleteHours() {
  try {
    viewModel.deleting = true;
    await workHourStore.deleteWorkHourById(viewModel.id);
    emit("saved", null);
    $q.notify({
      message: "Timeføring slettet",
      color: "positive",
    });
  } catch (error) {
    $q.notify({
      message: "Klarte ikke å slette timeføring",
      color: "negative",
    });
  } finally {
    viewModel.deleting = false;
  }
}

const descriptionIsValid = computed(
  () => viewModel.description && viewModel.description.trim() !== ""
);

const canDelete = computed(() => viewModel.status !== 1 && viewModel.id);

const canSave = computed(
  () => viewModel.status !== 1 && viewModel.status !== 2
);

const validateDescription = () => {
  viewModel.descriptionValid = descriptionIsValid.value;
};

onMounted(() => {
  if (props.modelValue) {
    const start = new Date(props.modelValue.startTime);
    const end = new Date(props.modelValue.endTime);
    viewModel.startDate = format(start, "dd.MM.yyyy");
    viewModel.startTime = format(start, "HH:mm");
    viewModel.endTime = format(end, "HH:mm");
    viewModel.description = props.modelValue.description;
    viewModel.id = props.modelValue.workHourId;
    viewModel.status = props.modelValue.approvalStatus;
    calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
  } else {
    calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
  }
});
</script>
