<template>
  <q-card class="full-width">
    <q-card-section class="text-h6"
      >Timeføring
      <q-badge v-if="viewModel.status === 1" color="positive">Godkjent</q-badge>
      <q-badge v-if="viewModel.status === 2" color="negative">Avvist</q-badge>
      <div v-if="modifiedByText" class="text-caption text-grey-7">
        {{ modifiedByText }}
      </div>
    </q-card-section>
    <q-card-actions v-if="canApprove" align="right">
      <q-btn
        no-caps
        unelevated
        color="negative"
        label="Avslå"
        @click="approveHours(2)"
        :disable="(hasChanges && !validForm) || busy"
        :loading="viewModel.approving === 2"
      ></q-btn>
      <q-btn
        no-caps
        unelevated
        color="positive"
        label="Godkjenn"
        @click="approveHours(1)"
        :disable="(hasChanges && !validForm) || busy"
        :loading="viewModel.approving === 1"
      ></q-btn>
    </q-card-actions>
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
        :disable="busy"
        :loading="viewModel.deleting"
      ></q-btn>
      <q-space></q-space>
      <q-btn
        no-caps
        flat
        label="Avbryt"
        @click="emit('cancel')"
        :disable="busy"
      ></q-btn>
      <q-btn
        v-if="canSave"
        no-caps
        unelevated
        color="primary"
        label="Lagre"
        @click="saveHours"
        :disable="!validForm || busy"
        :loading="viewModel.saving"
      ></q-btn>
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import { computed, ref, reactive, onMounted } from "vue";
import { useWorkHourStore } from "stores/WorkHourStore";
import { useAuthStore } from "src/stores/AuthStore";
import TimePickerInput from "./TimePickerInput.vue";
import DatePickerInput from "./DatePickerInput.vue";
import { useQuasar } from "quasar";
import {
  buildWorkHourPatch,
  getWorkHourChanges,
  getWorkHourErrorKind,
} from "src/shared/workHourDiff";
import { getApiErrorMessage } from "src/shared/apiError";
import {
  formatDate,
  formatDateTime,
  formatTime,
  intervalOn,
  isValidDate,
  isValidTime,
  toInstantWire,
} from "src/shared/time";
import type { ApprovalStatus, WorkHourValues } from "src/shared/workHourDiff";
import type { UpdateWorkHourRequest, WorkHourResponse } from "src/types";

interface TimeTrackingViewModel {
  id: number | null;
  startDateTime: string | null;
  endDateTime: string | null;
  startDate: string | null;
  // set*-funksjonene tilordner selve strengen (brukes som truthy-sjekk).
  startDateValid: boolean | string | null;
  startTime: string | null;
  startTimeValid: boolean | string | null;
  endTime: string | null;
  endTimeValid: boolean | string | null;
  description: string | null;
  // Settes fra descriptionIsValid, som er et `&&`-uttrykk (ikke ren boolean).
  descriptionValid: boolean | string | null;
  status: number | null | undefined;
  saving: boolean;
  deleting: boolean;
  approving: ApprovalStatus | null;
  loading: boolean;
}

const props = withDefaults(
  defineProps<{
    modelValue?: WorkHourResponse | null | undefined;
    // Viser «Godkjenn»/«Avslå» for admin på åpne føringer (brukes fra godkjenningssiden).
    allowApproval?: boolean;
  }>(),
  {
    modelValue: undefined,
    allowApproval: false,
  }
);

const emit = defineEmits<{
  cancel: [];
  saved: [value: WorkHourResponse | null];
}>();
const $q = useQuasar();
const workHourStore = useWorkHourStore();
const authStore = useAuthStore();
const isAdmin = computed(() => authStore.isAdmin);
const loading = ref(false);

const viewModel = reactive<TimeTrackingViewModel>({
  id: null,
  startDateTime: null,
  endDateTime: null,
  startDate: formatDate(new Date()),
  startDateValid: true,
  startTime: formatTime(new Date()),
  startTimeValid: true,
  endTime: formatTime(new Date()),
  endTimeValid: true,
  description: null,
  descriptionValid: true,
  status: 0,
  saving: false,
  deleting: false,
  approving: null,
  loading: false,
});

// Opprinnelige verdier (satt ved mount) for å finne hva som faktisk er endret.
const original = ref<WorkHourValues | null>(null);

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

function calculateTime(
  startDate: string | null,
  startTime: string | null,
  endTime: string | null
) {
  if (!startDate || !startTime || !endTime) return;
  // Hvert felt valideres for seg, slik at det er feltet med feil som
  // markeres (ikke starttid når det er datoen som er ugyldig).
  viewModel.startDateValid = isValidDate(startDate);
  viewModel.startTimeValid = isValidTime(startTime);
  viewModel.endTimeValid = isValidTime(endTime);
  if (!viewModel.startDateValid || !viewModel.startTimeValid) {
    viewModel.startDateTime = null;
    viewModel.endDateTime = null;
    return;
  }
  // Slutt før start betyr at føringen går over midnatt (neste dag).
  const { start, end } = intervalOn(startDate, startTime, endTime);
  viewModel.startDateTime = toInstantWire(start);
  viewModel.endDateTime = viewModel.endTimeValid ? toInstantWire(end) : null;
}

const endDate = computed(() => {
  if (!viewModel.endDateTime) return undefined;
  const endDate = formatDate(viewModel.endDateTime);
  return endDate != viewModel.startDate ? `Sluttdato: ${endDate}` : undefined;
});

const calculatedHours = computed(() => {
  if (!viewModel.startDateTime || !viewModel.endDateTime) return null;
  const start = new Date(viewModel.startDateTime);
  const end = new Date(viewModel.endDateTime);
  const diff = (end.getTime() - start.getTime()) / (1000 * 60 * 60); // Convert milliseconds to hours
  const hours = Math.floor(diff);
  const minutes = Math.round((diff - hours) * 60);
  return `${hours}:${minutes.toString().padStart(2, "0")}`;
});

const currentValues = computed(() => ({
  startDateTime: viewModel.startDateTime,
  endDateTime: viewModel.endDateTime,
  description: viewModel.description,
}));

const hasChanges = computed(
  () =>
    !original.value ||
    Object.keys(getWorkHourChanges(original.value, currentValues.value))
      .length > 0
);

const busy = computed(
  () => viewModel.saving || viewModel.deleting || viewModel.approving !== null
);

const modifiedByText = computed(() => {
  const model = props.modelValue;
  if (!model?.modifiedBy) return null;
  const name = model.modifiedByName ?? "ukjent";
  const time = model.modifiedTime
    ? ` ${formatDateTime(model.modifiedTime)}`
    : "";
  return `Endret av ${name}${time}`;
});

function validateContent() {
  if (
    !viewModel.startDateValid ||
    !viewModel.startTimeValid ||
    !viewModel.endTimeValid
  ) {
    $q.notify({
      message: "Vennligst sjekk at tidspunktene er gyldige",
      color: "negative",
    });
    return false;
  }
  if (!descriptionIsValid.value) {
    viewModel.descriptionValid = false;
    $q.notify({
      message: "Kommentar må fylles ut",
      color: "negative",
    });
    return false;
  }
  if (!calculatedHours.value || calculatedHours.value === "0:00") {
    $q.notify({
      message: "Null timer gidder vi ikke å lagre vel. 😝",
      color: "negative",
    });
    return false;
  }
  return true;
}

function notifyError(error: unknown, fallbackMessage: string) {
  const kind = getWorkHourErrorKind(error);
  const defaultMessage =
    kind === "conflict"
      ? "Føringen er allerede behandlet og kan ikke endres lenger"
      : kind === "notFound"
        ? "Føringen finnes ikke lenger"
        : kind === "forbidden"
          ? "Du har ikke tilgang til å endre denne føringen"
          : fallbackMessage;
  $q.notify({
    message: getApiErrorMessage(error, defaultMessage),
    color: "negative",
  });
  if (kind === "conflict" || kind === "notFound") {
    // Forelder lukker og laster listen på nytt.
    emit("saved", null);
  }
}

async function saveHours() {
  if (props.modelValue) {
    await updateHours();
  } else {
    if (!validateContent()) return;
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
    };
    const result = await workHourStore.createWorkHour(payload);
    emit("saved", result);
    $q.notify({
      message: "Timer lagret, bra jobba! 🙌",
      color: "positive",
    });
  } catch (error) {
    $q.notify({
      message: getApiErrorMessage(error, "Klarte ikke å lagre timer"),
      color: "negative",
    });
  } finally {
    viewModel.saving = false;
  }
}

const validForm = computed(() => {
  return (
    viewModel.startDateValid &&
    viewModel.startTimeValid &&
    viewModel.endTimeValid &&
    descriptionIsValid.value
  );
});

// updateHours/approveHours/deleteHours kjøres kun for en eksisterende føring
// (modelValue satt), så id og original er satt i onMounted.
// currentValues inneholder kun ISO-strenger/null, så patchen har aldri Date.
async function updateHours() {
  const changes = buildWorkHourPatch(original.value!, currentValues.value);
  if (Object.keys(changes).length === 0) {
    // Ingen endringer – ikke send tom PATCH.
    emit("cancel");
    return;
  }
  if (!validateContent()) return;
  try {
    viewModel.saving = true;
    const result = await workHourStore.patchWorkHour(
      viewModel.id!,
      changes as UpdateWorkHourRequest
    );
    emit("saved", result);
    $q.notify({
      message: "Endringer lagret",
      color: "positive",
    });
  } catch (error) {
    notifyError(error, "Klarte ikke å lagre endringer");
  } finally {
    viewModel.saving = false;
  }
}

async function approveHours(approvalStatus: ApprovalStatus) {
  const payload = buildWorkHourPatch(
    original.value!,
    currentValues.value,
    approvalStatus
  );
  const contentChanged = Object.keys(payload).some(
    (key) => key !== "approvalStatus"
  );
  if (contentChanged && !validateContent()) return;
  try {
    viewModel.approving = approvalStatus;
    const result = await workHourStore.patchWorkHour(
      viewModel.id!,
      payload as UpdateWorkHourRequest
    );
    emit("saved", result);
    $q.notify({
      message:
        approvalStatus === 1 ? "Timeføring godkjent" : "Timeføring avslått",
      color: "positive",
    });
  } catch (error) {
    notifyError(
      error,
      approvalStatus === 1
        ? "Klarte ikke å godkjenne timeføring"
        : "Klarte ikke å avslå timeføring"
    );
  } finally {
    viewModel.approving = null;
  }
}

async function deleteHours() {
  try {
    viewModel.deleting = true;
    await workHourStore.deleteWorkHourById(viewModel.id!);
    emit("saved", null);
    $q.notify({
      message: "Timeføring slettet",
      color: "positive",
    });
  } catch (error) {
    notifyError(error, "Klarte ikke å slette timeføring");
  } finally {
    viewModel.deleting = false;
  }
}

const descriptionIsValid = computed(
  () => viewModel.description && viewModel.description.trim() !== ""
);

const isOpen = computed(() => viewModel.status == null);

const canDelete = computed(() => !!viewModel.id && isOpen.value);

const canSave = computed(
  () => viewModel.status !== 1 && viewModel.status !== 2
);

const canApprove = computed(
  () => props.allowApproval && isAdmin.value && !!viewModel.id && isOpen.value
);

const validateDescription = () => {
  viewModel.descriptionValid = descriptionIsValid.value;
};

onMounted(() => {
  if (props.modelValue) {
    // startTime/endTime er nullable i DTO-en, men alltid satt på lagrede føringer.
    viewModel.startDate = formatDate(props.modelValue.startTime);
    viewModel.startTime = formatTime(props.modelValue.startTime);
    viewModel.endTime = formatTime(props.modelValue.endTime);
    // DTO-en har description som valgfri; null og undefined behandles likt i diffen.
    viewModel.description = props.modelValue.description ?? null;
    viewModel.id = props.modelValue.workHourId;
    viewModel.status = props.modelValue.approvalStatus;
    calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
    // Lagres etter calculateTime slik at uendret skjema gir tom diff
    // (også når servertiden har sekunder som skjemaet ikke viser).
    original.value = { ...currentValues.value };
  } else {
    calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
  }
});
</script>
