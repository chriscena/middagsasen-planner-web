<template>
  <q-card
    v-for="resource in event.resources"
    :key="resource.id"
    :class="resourceClasses(resource)"
    flat
  >
    <q-list role="list" separator>
      <q-item
        clickable
        dense
        class="q-py-sm text-bold"
        @click="showResourceInfo(resource)"
        ><q-item-section
          ><q-item-label lines="1"
            >{{ resource.resourceType.name }}
            <q-icon
              class="q-mr-xs"
              color="negative"
              name="warning"
              v-if="resource.isMissingStaff"
            ></q-icon
            ><q-icon
              v-if="resource.resourceType.files.length"
              color="blue-6"
              name="info"
              class="q-mr-xs"
            ></q-icon>
            <q-icon
              v-if="resource.competencyWarnings.length"
              color="amber-8"
              name="group_off"
              class="q-mr-xs"
              ><q-tooltip>
                <div
                  v-for="w in resource.competencyWarnings"
                  :key="w.competencyName"
                >
                  {{ w.competencyName }}: {{ w.currentCount }} av
                  {{ w.minimumRequired }} påkrevd
                </div>
              </q-tooltip></q-icon
            >
            <q-badge rounded color="yellow-8" v-if="resource.messages.length">
              {{ resource.messages.length }}</q-badge
            ></q-item-label
          > </q-item-section
        ><q-item-section side>{{
          formatTimeRange(resource.startTime, resource.endTime)
        }}</q-item-section></q-item
      >
      <q-item v-for="{ key, shift } in createShiftList(resource)" :key="key">
        <q-item-section v-if="isAdmin" avatar>
          <q-btn
            flat
            round
            icon="edit"
            size="sm"
            title="Endre vakt"
            @click="editShift(resource, shift)"
          ></q-btn>
        </q-item-section>
        <q-item-section>
          <q-item-label overline v-if="isVacant(shift)">Ledig</q-item-label>
          <q-item-label v-if="isTaken(shift)"
            ><q-icon
              v-if="shift.needsTraining"
              size="sm"
              name="escalator_warning"
            ></q-icon>

            {{ shift.user.fullName }}</q-item-label
          >
          <q-item-label caption v-if="isTaken(shift)">{{
            shift.comment
          }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <!-- Flaggene (isMine, canEdit, canConfirmTraining, canSignUp)
               beregnes av backend (ShiftRules) for innlogget bruker. -->
          <q-btn
            flat
            round
            icon="edit"
            title="Endre vakt"
            v-if="isTaken(shift) && shift.isMine && shift.canEdit"
            @click="edit(shift, resource)"
          ></q-btn>
          <!-- Admin bekrefter opplæring i sin egen dialog. -->
          <q-btn
            flat
            round
            icon="school"
            title="Bekreft opplæring"
            v-if="!isAdmin && isTaken(shift) && shift.canConfirmTraining"
            @click="confirmTraining(resource, shift)"
          ></q-btn>
          <q-btn
            flat
            round
            :aria-label="`Ring til ${shift.user.fullName}`"
            icon="call"
            type="a"
            :href="'tel:' + shift.user.phoneNumber"
            v-if="isTaken(shift) && !shift.isMine"
          ></q-btn>
          <q-btn
            flat
            round
            icon="add"
            :disable="adding"
            title="Ta vakt"
            v-if="isVacant(shift) && resource.canSignUp"
            @click="takeShift(resource)"
          ></q-btn>
        </q-item-section>
      </q-item>
    </q-list>
    <q-separator v-if="isAdmin && isFuture(timestamp.date)"></q-separator>
    <span v-if="isAdmin" class="row">
      <q-item
        v-if="isFuture(timestamp.date)"
        clickable
        v-ripple
        dense
        icon="add"
        class="q-pa-sm row"
        style="width: 50%; font-size: small"
        @click="addEmptyShift(resource)"
      >
        <q-icon name="add" size="sm" class="q-px-sm" />
        <q-item-section class="text-bold"> Legg til</q-item-section>
      </q-item>
      <q-separator vertical></q-separator>
      <q-item
        v-if="isFuture(timestamp.date)"
        clickable
        v-ripple
        dense
        icon="remove"
        class="q-pa-sm row"
        style="width: 49.5%; font-size: small"
        @click="deleteEmptyShift(resource)"
      >
        <q-icon name="remove" size="sm" class="q-px-sm" />
        <q-item-section class="text-bold"> Fjern</q-item-section>
      </q-item>
    </span>
  </q-card>

  <q-inner-loading :showing="adding">
    <q-spinner size="3em" color="primary"></q-spinner>
  </q-inner-loading>
  <!-- selectedShift og selectedResource er satt når
       dialogene under er åpne (q-dialog rendrer ikke innholdet når den er
       lukket), derav `!` i uttrykkene. -->
  <q-dialog v-model="showingEdit" persistent>
    <q-card class="full-width">
      <q-card-section class="text-h6 row"
        ><span> Redigere</span><q-space></q-space
        ><q-btn
          v-if="selectedShift!.canWithdraw"
          size="md"
          flat
          dense
          round
          icon="delete"
          color="negative"
          @click="withdrawShift"
        ></q-btn>
      </q-card-section>
      <!-- Vakter tatt før svaret ble påkrevd ved påmelding kan mangle
           opplæringsrad. Svaret lagres sammen med vakta (Lagre). -->
      <q-card-section
        class="text-center"
        v-if="selectedResource!.mustAnswerTraining"
        >Vi har ikke registrert at du har fått opplæring til denne type vakter,
        trenger du det?</q-card-section
      >
      <q-card-section
        class="row text-center q-gutter-sm"
        v-if="selectedResource!.mustAnswerTraining"
        ><div class="col-12">
          <q-btn
            @click="trainingComplete = false"
            unelevated
            :flat="trainingComplete !== false"
            :color="trainingComplete === false ? 'primary' : 'grey-9'"
            no-caps
            >Yes! Jeg trenger opplæring! 🙋‍♂️</q-btn
          >
        </div>
        <div class="col-12">
          <q-btn
            @click="trainingComplete = true"
            unelevated
            :flat="trainingComplete !== true"
            :color="trainingComplete === true ? 'primary' : 'grey-9'"
            no-caps
            >Allerede fått opplæring, full kontroll! ✌️</q-btn
          >
        </div></q-card-section
      >
      <q-card-section class="row q-gutter-sm">
        <q-input
          class="col-12"
          autofocus
          outlined
          label="Kommentar"
          v-model="selectedShift!.comment"
        ></q-input>
        <div
          class="col-12 row items-right items-center"
          v-if="
            !selectedResource!.mustAnswerTraining &&
            selectedResource!.resourceType.hasTraining
          "
        >
          <div class="q-mr-md">Fått opplæring</div>
          <q-btn-toggle
            disable
            v-model="trainingComplete"
            toggle-color="primary"
            :options="[
              { label: 'Ja', value: true },
              { label: 'Nei', value: false },
            ]"
          /></div
      ></q-card-section>
      <q-card-actions align="right">
        <q-btn flat label="Avbryt" no-caps @click="showingEdit = false"></q-btn>
        <q-btn
          unelevated
          color="primary"
          label="Lagre"
          no-caps
          @click="saveOwnShift"
        ></q-btn> </q-card-actions
      ><q-inner-loading :showing="saving">
        <q-spinner size="3em" color="primary"></q-spinner>
      </q-inner-loading>
    </q-card>
  </q-dialog>
  <!-- Kun for admin. Trenere bekrefter opplæring i en egen dialog. -->
  <q-dialog v-model="showingAdminEdit" persistent>
    <q-card class="full-width">
      <q-card-section class="text-h6 row"
        ><span> Endre vakt</span><q-space></q-space
        ><q-btn
          v-if="selectedShift!.id > 0 && selectedShift!.canWithdraw"
          size="md"
          flat
          dense
          round
          icon="delete"
          color="negative"
          @click="withdrawShift"
        ></q-btn>
      </q-card-section>
      <q-card-section class="q-gutter-sm">
        <q-select
          label="Navn"
          autofocus
          outlined
          :options="users"
          :loading="loadingUsers"
          option-label="fullName"
          option-value="id"
          v-model="selectedShift!.user"
          @update:model-value="onUserUpdated"
        >
          <template v-slot:option="scope">
            <q-item v-bind="scope.itemProps">
              <q-item-section>
                {{ scope.opt.fullName }}
              </q-item-section>
              <q-item-section side>
                <q-item-label caption>{{ scope.opt.phoneNo }}</q-item-label>
              </q-item-section>
            </q-item>
          </template>
        </q-select>
        <q-input
          outlined
          label="Kommentar"
          v-model="selectedShift!.comment"
        ></q-input>
        <div
          class="row items-right items-center"
          v-if="selectedResource!.resourceType.hasTraining"
        >
          <div class="q-mr-md">Fått opplæring</div>
          <q-btn-toggle
            v-model="trainingComplete"
            :disable="!selectedShift!.user"
            toggle-color="primary"
            :options="[
              { label: 'Ja', value: true },
              { label: 'Nei', value: false },
            ]"
          />
          <div v-if="mustChooseTraining" class="col-12 text-caption">
            Brukeren har ikke svart på om hen trenger opplæring. Velg Ja eller
            Nei.
          </div>
        </div></q-card-section
      >
      <q-card-actions align="right">
        <q-btn
          flat
          label="Avbryt"
          no-caps
          @click="showingAdminEdit = false"
        ></q-btn>
        <q-btn
          unelevated
          color="primary"
          label="Lagre"
          no-caps
          @click="saveAdminShift"
          :disable="!selectedShift!.user || mustChooseTraining"
        ></q-btn> </q-card-actions
      ><q-inner-loading :showing="saving">
        <q-spinner size="3em" color="primary"></q-spinner>
      </q-inner-loading>
    </q-card>
  </q-dialog>
  <q-dialog v-model="showingTrainingDialog" persistent>
    <q-card class="text-center">
      <q-card-section
        >Vi har ikke registrert at du har fått opplæring til denne type vakter,
        trenger du det?</q-card-section
      >
      <q-card-section class="q-gutter-md"
        ><q-btn
          @click="signUpSelf(selectedResource!, true)"
          unelevated
          color="primary"
          no-caps
          >Yes! Jeg trenger opplæring! 🙋‍♂️</q-btn
        ></q-card-section
      >
      <q-card-section
        ><q-btn flat no-caps @click="signUpSelf(selectedResource!, false)"
          >Allerede fått opplæring, full kontroll! ✌️</q-btn
        ></q-card-section
      >
      <!-- Avbryt = ingen påmelding. -->
      <q-card-actions align="right">
        <q-btn
          flat
          label="Avbryt"
          no-caps
          @click="showingTrainingDialog = false"
        ></q-btn>
      </q-card-actions>
    </q-card>
  </q-dialog>
  <q-dialog v-model="showingConfirmTraining" persistent>
    <q-card>
      <q-card-section
        >Bekrefte at {{ selectedShift!.user?.fullName }} har fått opplæring på
        {{ selectedResource!.resourceType.name }}?</q-card-section
      >
      <q-card-actions align="right">
        <q-btn
          flat
          label="Avbryt"
          no-caps
          @click="showingConfirmTraining = false"
        ></q-btn>
        <q-btn
          unelevated
          color="primary"
          label="Bekreft"
          no-caps
          @click="saveConfirmTraining"
        ></q-btn> </q-card-actions
      ><q-inner-loading :showing="saving">
        <q-spinner size="3em" color="primary"></q-spinner>
      </q-inner-loading>
    </q-card>
  </q-dialog>

  <q-dialog v-model="showingResourceInfo" :maximized="$q.platform.is.mobile">
    <q-card
      :style="
        $q.platform.is.desktop
          ? 'max-width: 600px;min-width: 400px;width: 80vw;height: 80vw;'
          : ''
      "
    >
      <q-card-section class="row">
        <q-btn
          flat
          dense
          round
          icon="close"
          @click="showingResourceInfo = false"
          title="Lukk"
        ></q-btn>
        <div class="text-h6">
          {{ selectedResource!.resourceType.name }}
        </div>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-section>
        <q-card
          flat
          bordered
          v-if="selectedResource!.resourceType.files?.length"
        >
          <q-card-section class="q-py-sm text-subtitle2">
            Nyttig info
          </q-card-section>
          <q-separator></q-separator>
          <q-list>
            <q-item
              v-for="file in selectedResource!.resourceType.files"
              :key="file.id"
              clickable
              @click="downloadResourceTypeFileOrNotify(file, $q.notify)"
            >
              <q-item-section
                ><q-item-label lines="1">{{
                  file.description
                }}</q-item-label></q-item-section
              >
              <q-item-section side
                ><q-icon name="download"></q-icon> </q-item-section
            ></q-item> </q-list></q-card
      ></q-card-section>
      <q-card-section>
        <q-card flat class="bg-yellow-2">
          <q-card-section class="q-py-sm text-subtitle2">
            Beskjed til vakta</q-card-section
          >
          <q-separator></q-separator>
          <q-list separator>
            <q-item
              v-for="message in selectedResource!.messages"
              :key="message.id"
            >
              <q-item-section>
                <q-item-label overline>{{
                  formatShortDate(message.created)
                }}</q-item-label>
                <q-item-label class="q-py-sm">{{
                  message.message
                }}</q-item-label>
                <q-item-label caption>{{
                  message.createdBy.fullName
                }}</q-item-label>
              </q-item-section>

              <q-item-section side top>
                <q-btn
                  flat
                  round
                  size="sm"
                  icon="delete"
                  @click="deleteMessage(message)"
                  :disable="!canDeleteMessage(message)"
                  :loading="deletingMessage"
                ></q-btn>
              </q-item-section>
            </q-item>
          </q-list>
          <template v-if="!isPastDay">
            <q-card-section>
              <q-input
                outlined
                label="Ny beskjed"
                autogrow
                type="textarea"
                v-model="newMessage"
                :maxlength="MESSAGE_MAX_LENGTH"
                counter
              ></q-input>
            </q-card-section>
            <q-card-actions
              ><q-btn
                label="Nullstill"
                @click="newMessage = null"
                no-caps
                flat
                :disable="savingMessage"
              ></q-btn
              ><q-space></q-space>
              <q-btn
                label="Lagre"
                @click="saveMessage"
                color="primary"
                no-caps
                unelevated
                :disable="!newMessage?.trim()"
                :loading="savingMessage"
              ></q-btn> </q-card-actions
          ></template>
        </q-card>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useQuasar } from "quasar";
import type { Timestamp } from "@timestamp-js/core";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";
import { useAuthStore } from "stores/AuthStore";
import { getApiErrorMessage } from "src/shared/apiError";
import { downloadResourceTypeFileOrNotify } from "src/shared/fileDownload";
import { createShiftList, type ShiftListItem } from "src/shared/shiftList";
import {
  formatShortDate,
  formatTimeRange,
  isFuture,
  isPast,
} from "src/shared/time";
import type {
  EventResponse,
  MessageRequest,
  MessageResponse,
  ResourceResponse,
  ShiftResponse,
  ShiftUserResponse,
  UserResponse,
} from "src/types";

// Maks lengde på en beskjed; speiler MessageRequest.MaxLength i backend.
const MESSAGE_MAX_LENGTH = 4000;

// Dagen fra q-calendar-agenda; kun date (yyyy-MM-dd) brukes.
type DayTimestamp = Pick<Timestamp, "date">;

// Bruker på en vakt under redigering: fra vakta selv, eller valgt i q-select
// (admin), som har userStore.users (UserResponse) som options.
type ShiftUser = ShiftUserResponse | UserResponse;

// Kopi av vakta (eller den ledige plassen, id 0) som redigeres i dialogene.
interface EditableShift {
  id: number;
  user: ShiftUser | null;
  comment?: null | string;
  canWithdraw?: boolean;
}

// props and emits
// NB: modelValue og timestamp hadde `require: true` (skrivefeil for
// `required`) i JS-versjonen; de er påkrevd her, slik det var ment.
const props = withDefaults(
  defineProps<{
    modelValue: EventResponse;
    // authStore.isAdmin kan være undefined; da brukes default (false).
    isAdmin?: boolean | undefined;
    timestamp: DayTimestamp;
  }>(),
  { isAdmin: false }
);

// Storeinit
const $q = useQuasar();
const isAdmin = computed(() => props.isAdmin);
const eventStore = useEventStore();
const authStore = useAuthStore();
const userStore = useUserStore();

//Refs
const adding = ref(false);
const deletingMessage = ref(false);
const loading = ref(false);
const loadingUsers = ref(false);
const newMessage = ref<string | null>(null);
const saving = ref(false);
const savingMessage = ref(false);
const selectedResource = ref<ResourceResponse | null>(null);
const selectedShift = ref<EditableShift | null>(null);
// Opplæringsstatus i dialogene for brukeren på vakta (null = ikke svart).
// initialTrainingComplete er verdien da dialogen ble åpnet / brukeren valgt,
// så setTraining bare kalles når den er endret.
const trainingComplete = ref<boolean | null>(null);
const initialTrainingComplete = ref<boolean | null>(null);
// Om brukeren på vakta har en opplæringsrad for ressurstypen.
const selectedUserHasTraining = ref(false);
// Brukeren på vakta da admin-dialogen ble åpnet (null for ledig plass), så
// det kan avgjøres om admin bytter bruker.
const originalUserId = ref<number | null>(null);
const showingAdminEdit = ref(false);
const showingConfirmTraining = ref(false);
const showingEdit = ref(false);
const showingResourceInfo = ref(false);
const showingTrainingDialog = ref(false);

// Computed
const users = computed(() => userStore.users);
// Dagen i kalenderen er før i dag.
const isPastDay = computed(() => isPast(props.timestamp.date));
const event = computed(() => props.modelValue);
// Komponenten vises kun for innloggede brukere (IndexPage krever innlogging).
const currentUser = computed(() => authStore.user!);
// Admin setter opp en bruker (på en ledig plass eller ved bytte av bruker på
// en vakt) som ikke har svart på opplæring: svaret er påkrevd av backend, så
// Lagre er deaktivert til Ja/Nei er valgt.
const isNewUser = computed(
  () =>
    !!selectedShift.value?.user &&
    (selectedShift.value.id === 0 ||
      selectedShift.value.user.id !== originalUserId.value)
);
const mustChooseTraining = computed(
  () =>
    showingAdminEdit.value &&
    isNewUser.value &&
    !!selectedResource.value?.resourceType.hasTraining &&
    !selectedUserHasTraining.value &&
    trainingComplete.value === null
);

// Methods
function resourceClasses(resource: ResourceResponse): string {
  return (
    "q-mt-sm q-mx-sm " +
    (resource.isPast
      ? "bg-grey-3"
      : resource.isMissingStaff
      ? "bg-red-1"
      : "bg-green-1")
  );
}

// `?? false` er fjernet: venstresiden er alltid en boolean.
function isVacant(shift: ShiftListItem): boolean {
  return (shift?.user?.id ?? 0) === 0;
}

// Type guard: en tatt vakt er en ShiftResponse med bruker. `?? 0` i stedet
// for `?? false`: `undefined > 0` og `0 > 0` gir begge false.
function isTaken(shift: ShiftListItem): shift is ShiftResponse {
  return (shift?.user?.id ?? 0) > 0;
}

// Advarsler fra backend som ikke stoppet endringen (f.eks. at SMS til
// trenerne feilet).
function notifyWarnings(warnings: string[]): void {
  for (const warning of warnings) {
    $q.notify({ type: "warning", message: warning, multiLine: true });
  }
}

function notifyError(error: unknown, fallback: string): void {
  console.log(error);
  $q.notify({ message: getApiErrorMessage(error, fallback) });
}

// Leser opplæringen til brukeren på ressursens ressurstype inn i dialogene.
function loadTraining(
  user: ShiftUser | null,
  resource: ResourceResponse
): void {
  // trainings er nullable i UserResponse.
  const trainings: {
    resourceTypeId: number;
    trainingComplete?: boolean | null;
  }[] = user?.trainings ?? [];
  const training = trainings.find(
    (t) => t.resourceTypeId === resource.resourceType.id
  );
  selectedUserHasTraining.value = !!training;
  trainingComplete.value = training?.trainingComplete ?? null;
  initialTrainingComplete.value = trainingComplete.value;
}

function isTrainingChanged(): boolean {
  return (
    !!selectedResource.value?.resourceType.hasTraining &&
    trainingComplete.value !== null &&
    trainingComplete.value !== initialTrainingComplete.value
  );
}

function closeDialogs(): void {
  showingEdit.value = false;
  showingAdminEdit.value = false;
  showingConfirmTraining.value = false;
}

// «Ta vakt» på en ledig plass. Har ressurstypen opplæring og brukeren ikke
// svart før (mustAnswerTraining), spørres det først; svaret sendes med i
// samme påmelding.
async function takeShift(resource: ResourceResponse): Promise<void> {
  selectedResource.value = resource;
  if (resource.mustAnswerTraining) {
    showingTrainingDialog.value = true;
    return;
  }
  await signUpSelf(resource);
}

async function signUpSelf(
  resource: ResourceResponse,
  needsTraining?: boolean
): Promise<void> {
  showingTrainingDialog.value = false;
  try {
    adding.value = true;
    if (resource.resourceType.notificationMessage) {
      $q.notify({
        icon: "campaign",
        iconColor: "primary",
        message: resource.resourceType.notificationMessage,
        position: "center",
        multiLine: true,
      });
    }
    const result = await eventStore.signUp(
      resource.id,
      needsTraining === undefined ? {} : { needsTraining }
    );
    $q.notify({
      message: "Woohoo! Du har tatt en vakt 🎉",
    });
    notifyWarnings(result.warnings);
  } catch (error) {
    notifyError(error, "Oh no! Noe tryna da du skulle ta vakta! 🙈");
  } finally {
    adding.value = false;
  }
}

// Kalles fra admin-dialogen når en bruker er valgt i q-select (ikke clearable),
// så selectedShift, dens user og selectedResource er satt.
function onUserUpdated(): void {
  loadTraining(selectedShift.value!.user, selectedResource.value!);
}

// Kalles kun for brukerens egen vakt (canEdit), så user er satt.
function edit(shift: ShiftResponse, resource: ResourceResponse): void {
  selectedResource.value = resource;
  selectedShift.value = Object.assign({}, shift);
  loadTraining(shift.user, resource);
  showingEdit.value = true;
}

// Eiers redigeringsdialog: kommentaren lagres med changeShift. Svarte eieren
// på opplæringsspørsmålet (vakter uten opplæringsrad), lagres svaret med
// setTraining etterpå.
async function saveOwnShift(): Promise<void> {
  const shift = selectedShift.value!;
  const warnings: string[] = [];
  try {
    saving.value = true;
    const result = await eventStore.changeShift(shift.id, {
      comment: shift.comment ?? null,
    });
    warnings.push(...result.warnings);
    if (isTrainingChanged()) {
      const trainingResult = await eventStore.setTraining(
        shift.id,
        trainingComplete.value!
      );
      warnings.push(...trainingResult.warnings);
      initialTrainingComplete.value = trainingComplete.value;
    }
    closeDialogs();
    $q.notify({ message: "Endringer er lagret 👍" });
  } catch (error) {
    notifyError(error, "Oh no! Noe tryna da vi skulle lagre endringene! 🙈");
  } finally {
    saving.value = false;
    notifyWarnings(warnings);
  }
}

// Admin-dialogen. Ledig plass (id 0): signUp for valgt bruker. Eksisterende
// vakt: changeShift (evt. med ny bruker). Mangler en ny bruker opplæringsrad,
// sendes svaret på opplæring (needsTraining) med i samme kall. Til slutt
// setTraining hvis opplæringsstatusen for en eksisterende rad er endret.
// Kallene gjøres etter hverandre; feiler ett, vises feilen og dialogen
// blir stående med det som allerede er lagret.
async function saveAdminShift(): Promise<void> {
  const shift = selectedShift.value!;
  const resource = selectedResource.value!;
  // Lagre er deaktivert uten bruker.
  const userId = shift.user!.id;
  const warnings: string[] = [];
  // Svaret på opplæring for en ny bruker uten opplæringsrad (påkrevd av
  // backend, Lagre er deaktivert til det er valgt).
  const answersTraining =
    isNewUser.value &&
    !!resource.resourceType.hasTraining &&
    !selectedUserHasTraining.value &&
    trainingComplete.value !== null;
  try {
    saving.value = true;
    if (shift.id === 0) {
      const result = await eventStore.signUp(resource.id, {
        userId,
        comment: shift.comment ?? null,
        // Ignoreres av backend når brukeren allerede har en opplæringsrad.
        needsTraining:
          trainingComplete.value === null ? null : !trainingComplete.value,
      });
      warnings.push(...result.warnings);
      // Et nytt forsøk etter en feil under skal endre vakta, ikke ta den igjen.
      shift.id =
        result.resource.shifts.find((s) => s.user.id === userId)?.id ?? 0;
    } else {
      const result = await eventStore.changeShift(shift.id, {
        userId,
        comment: shift.comment ?? null,
        ...(answersTraining ? { needsTraining: !trainingComplete.value } : {}),
      });
      warnings.push(...result.warnings);
    }
    // Vakta tilhører nå valgt bruker.
    originalUserId.value = userId;
    // Manglet brukeren opplæringsrad, ble svaret lagret sammen med vakta.
    if (answersTraining) {
      selectedUserHasTraining.value = true;
      initialTrainingComplete.value = trainingComplete.value;
    }
    if (shift.id > 0 && isTrainingChanged()) {
      const result = await eventStore.setTraining(
        shift.id,
        trainingComplete.value!
      );
      warnings.push(...result.warnings);
      initialTrainingComplete.value = trainingComplete.value;
    }
    closeDialogs();
    $q.notify({ message: "Endringer er lagret 👍" });
  } catch (error) {
    notifyError(error, "Oh no! Noe tryna da vi skulle lagre endringene! 🙈");
  } finally {
    saving.value = false;
    notifyWarnings(warnings);
  }
}

// Kalles fra dialogene, der selectedShift er en eksisterende vakt.
async function withdrawShift(): Promise<void> {
  try {
    saving.value = true;
    await eventStore.withdraw(selectedShift.value!.id);
    closeDialogs();
    $q.notify({
      message: "Ajaj! Du har tatt bort vakta 😱",
    });
  } catch (error) {
    notifyError(error, "Oh no! Noe tryna da vi skulle ta bort vakta... 🙈");
  } finally {
    saving.value = false;
  }
}

// Trener for ressurstypen: bekrefte at eieren av vakta har fått opplæring.
function confirmTraining(
  resource: ResourceResponse,
  shift: ShiftResponse
): void {
  selectedResource.value = resource;
  selectedShift.value = Object.assign({}, shift);
  showingConfirmTraining.value = true;
}

async function saveConfirmTraining(): Promise<void> {
  try {
    saving.value = true;
    const result = await eventStore.setTraining(selectedShift.value!.id, true);
    closeDialogs();
    $q.notify({ message: "Opplæringen er bekreftet 👍" });
    notifyWarnings(result.warnings);
  } catch (error) {
    notifyError(error, "Oh no! Noe tryna da vi skulle lagre opplæringen! 🙈");
  } finally {
    saving.value = false;
  }
}

async function editShift(
  resource: ResourceResponse,
  shift: ShiftListItem
): Promise<void> {
  selectedShift.value = Object.assign({}, shift);
  selectedResource.value = resource;
  originalUserId.value = shift.user?.id ?? null;
  loadTraining(shift.user, resource);
  showingAdminEdit.value = true;
  if (isAdmin.value) await getUsers();
}

async function getUsers(): Promise<void> {
  try {
    loadingUsers.value = true;
    await userStore.getUsers();
  } catch {
    $q.notify({ message: "Klarte ikke å hente lista over brukere." });
  } finally {
    loadingUsers.value = false;
  }
}

function showResourceInfo(resource: ResourceResponse): void {
  selectedResource.value = resource;
  showingResourceInfo.value = true;
}

// Kalles fra ressursinfo-dialogen, der selectedResource er satt.
async function saveMessage(): Promise<void> {
  // Hver beskjed er en egen rad i API-et (sletting er et eget endepunkt), så en
  // tom beskjed har ingen mening og sendes ikke. Lagre er deaktivert da.
  const message = newMessage.value?.trim();
  if (!message) return;
  try {
    savingMessage.value = true;
    const model: MessageRequest = { message };
    const response = await eventStore.addMessage(
      selectedResource.value!.id,
      model
    );
    selectedResource.value!.messages.push(response);
    newMessage.value = null;
    $q.notify({ message: "Beskjeden er lagret. 📨" });
  } catch (error) {
    $q.notify({
      message: getApiErrorMessage(error, "Klarte ikke å lagre beskjed. 😿"),
    });
    console.log(error);
  } finally {
    savingMessage.value = false;
  }
}

// Kalles fra ressursinfo-dialogen, der selectedResource er satt.
async function deleteMessage(message: MessageResponse): Promise<void> {
  try {
    deletingMessage.value = true;
    await eventStore.deleteMessage(message);
    selectedResource.value!.messages = selectedResource.value!.messages.filter(
      (m) => m.id !== message.id
    );
    newMessage.value = null;
    $q.notify({ message: "Beskjeden er slettet. 📤" });
  } catch (error) {
    $q.notify({
      message: getApiErrorMessage(error, "Klarte ikke å slette beskjed. 😿"),
    });
    console.log(error);
  } finally {
    deletingMessage.value = false;
  }
}

function canDeleteMessage(message: MessageResponse): boolean {
  return (
    !isPastDay.value &&
    (isAdmin.value || message.createdBy.id === currentUser.value.id)
  );
}
// patchMinimumStaff legger svaret (ressursen med flagg) i cachen, så her
// regnes bare ut ny verdi som sendes.
async function addEmptyShift(resource: ResourceResponse): Promise<void> {
  try {
    loading.value = true;

    const minimumStaff =
      resource.minimumStaff < resource.shifts.length
        ? resource.shifts.length + 1
        : resource.minimumStaff + 1;
    await eventStore.patchMinimumStaff(resource.id, minimumStaff);
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      message: "errorOccurred",
    });
  } finally {
    loading.value = false;
  }
}
async function deleteEmptyShift(resource: ResourceResponse): Promise<void> {
  try {
    loading.value = true;

    const minimumStaff =
      resource.minimumStaff > resource.shifts.length
        ? resource.minimumStaff - 1
        : resource.minimumStaff;
    await eventStore.patchMinimumStaff(resource.id, minimumStaff);
  } catch (e) {
    console.error(e);
    $q.notify({
      type: "negative",
      message: "errorOccurred",
    });
  } finally {
    loading.value = false;
  }
}
</script>
