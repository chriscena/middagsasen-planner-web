<template>
  <q-card class="full-width">
    <q-card-section class="text-h6"
      >Timeføring
      <q-badge
        v-if="statusDisplay.badgeColor"
        :color="statusDisplay.badgeColor"
        >{{ statusDisplay.label }}</q-badge
      >
      <div v-if="createdByText" class="text-caption text-grey-7">
        {{ createdByText }}
      </div>
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
        @click="approveHours(ApprovalStatus.Rejected)"
        :disable="(hasChanges && !validForm) || busy"
        :loading="viewModel.approving === ApprovalStatus.Rejected"
      ></q-btn>
      <q-btn
        no-caps
        unelevated
        color="positive"
        label="Godkjenn"
        @click="approveHours(ApprovalStatus.Approved)"
        :disable="(hasChanges && !validForm) || busy"
        :loading="viewModel.approving === ApprovalStatus.Approved"
      ></q-btn>
    </q-card-actions>
    <q-card-section class="q-gutter-sm">
      <q-select
        v-if="showOwnerSelect"
        outlined
        use-input
        input-debounce="0"
        label="Bruker"
        :model-value="viewModel.ownerId"
        :options="ownerOptions"
        option-label="fullName"
        option-value="id"
        emit-value
        map-options
        :disable="loading || busy"
        :rules="[(val) => val != null || 'Velg bruker']"
        @filter="filterOwners"
        @update:model-value="(val) => (viewModel.ownerId = val ?? null)"
      >
        <template #no-option>
          <q-item>
            <q-item-section class="text-grey"> Ingen treff </q-item-section>
          </q-item>
        </template>
      </q-select>
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
        :outline="showOwnerSelect"
        :unelevated="!showOwnerSelect"
        color="primary"
        label="Lagre"
        @click="saveHours"
        :disable="!validForm || busy"
        :loading="viewModel.saving"
      ></q-btn>
      <q-btn
        v-if="showOwnerSelect"
        no-caps
        unelevated
        color="positive"
        label="Lagre og godkjenn"
        @click="saveAndApproveHours"
        :disable="!validForm || busy"
        :loading="viewModel.savingApproved"
      ></q-btn>
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import { computed, ref, reactive, onMounted } from "vue";
import { useWorkHourStore } from "@/stores/WorkHourStore";
import TimePickerInput from "./TimePickerInput.vue";
import DatePickerInput from "./DatePickerInput.vue";
import { useQuasar } from "quasar";
import {
  approvalAction,
  buildWorkHourPatch,
  getApprovalActionText,
  getApprovalStatusDisplay,
  getCreatedByText,
  getWorkHourChanges,
  getWorkHourError,
  hasDuration,
} from "@/shared/workHours";
import type { WorkHourAction, WorkHourValues } from "@/shared/workHours";
import {
  formatDate,
  formatDateTime,
  formatDuration,
  formatTime,
  intervalOn,
  isValidDate,
  isValidTime,
  toInstantWire,
} from "@/shared/time";
import { ApprovalStatus } from "@/types";
import type {
  CreateWorkHourRequest,
  UpdateWorkHourRequest,
  UserResponse,
  WorkHourResponse,
} from "@/types";
import { notifyApiError } from "@/shared/notifyApiError";
import { useUserStore } from "@/stores/UserStore";
import { filterUsersByName } from "@/shared/users";

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
  // null = ubehandlet (også for ny føring).
  status: ApprovalStatus | null;
  // Eieren av en ny føring når admin registrerer for en annen (selectOwner).
  ownerId: number | null;
  saving: boolean;
  // «Lagre og godkjenn» pågår.
  savingApproved: boolean;
  deleting: boolean;
  approving: ApprovalStatus | null;
  loading: boolean;
}

const props = withDefaults(
  defineProps<{
    modelValue?: WorkHourResponse | null | undefined;
    // Viser «Godkjenn»/«Avslå» der føringen kan godkjennes (`canApprove` fra
    // serveren). Brukes fra godkjenningssiden.
    allowApproval?: boolean;
    // Ny føring for en annen bruker (admin, fra godkjenningssiden): viser et
    // søkbart brukervalg og «Lagre og godkjenn». Gjelder ikke redigering.
    // Brukerlisten (bare aktive brukere) hentes fra UserStore av siden.
    selectOwner?: boolean;
    // Forhåndsvalgt eier i brukervalget, f.eks. brukeren siden er filtrert på.
    defaultOwnerId?: number | null;
  }>(),
  {
    modelValue: undefined,
    allowApproval: false,
    selectOwner: false,
    defaultOwnerId: null,
  }
);

const emit = defineEmits<{
  cancel: [];
  saved: [value: WorkHourResponse | null];
}>();
const $q = useQuasar();
const workHourStore = useWorkHourStore();
const userStore = useUserStore();
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
  status: null,
  ownerId: null,
  saving: false,
  savingApproved: false,
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

const calculatedHours = computed(() =>
  formatDuration(viewModel.startDateTime, viewModel.endDateTime)
);

const statusDisplay = computed(() =>
  getApprovalStatusDisplay(viewModel.status)
);

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
  () =>
    viewModel.saving ||
    viewModel.savingApproved ||
    viewModel.deleting ||
    viewModel.approving !== null
);

// Brukervalget vises bare for en ny føring.
const showOwnerSelect = computed(() => props.selectOwner && !props.modelValue);

const ownerOptions = ref<UserResponse[]>(userStore.users);

function filterOwners(val: string, update: (callbackFn: () => void) => void) {
  update(() => {
    ownerOptions.value = filterUsersByName(userStore.users, val);
  });
}

const createdByText = computed(() =>
  props.modelValue ? getCreatedByText(props.modelValue) : null
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
  if (!hasDuration(viewModel.startDateTime, viewModel.endDateTime)) {
    $q.notify({
      message: "Null timer gidder vi ikke å lagre vel. 😝",
      color: "negative",
    });
    return false;
  }
  return true;
}

function handleWorkHourError(error: unknown, action: WorkHourAction) {
  const { fallback, shouldReload } = getWorkHourError(error, action);
  // Fallback-teksten avhenger av 409/403/404; backendens melding vinner.
  notifyApiError(error, fallback);
  if (shouldReload) {
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

async function saveAndApproveHours() {
  if (!validateContent()) return;
  await createHours(true);
}

// approve = «Lagre og godkjenn» (bare i selectOwner-modus).
async function createHours(approve = false) {
  try {
    if (approve) viewModel.savingApproved = true;
    else viewModel.saving = true;
    const payload: CreateWorkHourRequest = {
      startTime: viewModel.startDateTime,
      endTime: viewModel.endDateTime,
      description: viewModel.description,
    };
    if (showOwnerSelect.value) {
      payload.userId = viewModel.ownerId;
      payload.approve = approve;
    }
    const result = await workHourStore.createWorkHour(payload);
    emit("saved", result);
    $q.notify({
      message: showOwnerSelect.value
        ? approve
          ? "Timer lagret og godkjent"
          : "Timer lagret"
        : "Timer lagret, bra jobba! 🙌",
      color: "positive",
    });
  } catch (error) {
    handleWorkHourError(error, "create");
  } finally {
    viewModel.saving = false;
    viewModel.savingApproved = false;
  }
}

const validForm = computed(() => {
  return (
    (!showOwnerSelect.value || viewModel.ownerId !== null) &&
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
    handleWorkHourError(error, "update");
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
      message: getApprovalActionText(approvalStatus).done,
      color: "positive",
    });
  } catch (error) {
    handleWorkHourError(error, approvalAction(approvalStatus));
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
    handleWorkHourError(error, "delete");
  } finally {
    viewModel.deleting = false;
  }
}

const descriptionIsValid = computed(
  () => viewModel.description && viewModel.description.trim() !== ""
);

// Tilgang avgjøres av serveren (flaggene i WorkHourResponse). En ny føring
// (uten modelValue) kan alltid lagres, men ikke slettes eller godkjennes.
const canSave = computed(() => props.modelValue?.canEdit ?? true);

const canDelete = computed(() => props.modelValue?.canDelete ?? false);

const canApprove = computed(
  () => props.allowApproval && (props.modelValue?.canApprove ?? false)
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
    viewModel.status = props.modelValue.approvalStatus ?? null;
    calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
    // Lagres etter calculateTime slik at uendret skjema gir tom diff
    // (også når servertiden har sekunder som skjemaet ikke viser).
    original.value = { ...currentValues.value };
  } else {
    // Forhåndsvelg bare en bruker som finnes i listen (aktiv).
    if (
      showOwnerSelect.value &&
      userStore.users.some((u) => u.id === props.defaultOwnerId)
    ) {
      viewModel.ownerId = props.defaultOwnerId;
    }
    calculateTime(viewModel.startDate, viewModel.startTime, viewModel.endTime);
  }
});
</script>
