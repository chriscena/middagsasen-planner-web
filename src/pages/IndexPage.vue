<template>
  <q-page padding v-touch-swipe.mouse.horizontal="handleSwipe">
    <q-header>
      <q-toolbar>
        <q-btn
          title="Meny"
          dense
          flat
          round
          icon="menu"
          @click="emit('toggle-left')"
        ></q-btn>
        <img
          src="~assets/middagsasen-banner-white.svg"
          class="q-ml-sm"
          style="max-height: 40px; max-width: 50vw"
        />
        <q-space></q-space>
        <q-btn
          title="Superfrivillige!"
          class="q-mr-sm"
          dense
          flat
          round
          icon="emoji_events"
          @click="showHallOfFame"
          color="amber"
        ></q-btn>
        <q-btn
          title="Din brukerinfo"
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
        ></q-btn>
      </q-toolbar>
    </q-header>
    <q-calendar-agenda
      locale="no"
      :view="mode"
      v-model="selectedDay"
      date-type="rounded"
      :weekdays="[1, 2, 3, 4, 5, 6, 0]"
      @change="onChange"
      @click-head-day="showMenu"
      animated
      ref="calendar"
    >
      <template #head-days-events>
        <q-linear-progress
          color="primary"
          size="xs"
          v-show="loading"
          indeterminate
        ></q-linear-progress>
      </template>
      <template #day="{ scope: { timestamp } }">
        <template v-for="event in getEventsForDate(timestamp)" :key="event.id">
          <q-card class="q-mt-sm q-mx-sm" flat bordered>
            <q-card-section class="q-py-sm text-bold row">
              <span class="col">
                <q-btn
                  flat
                  round
                  title="Redigere vaktliste"
                  icon="edit"
                  v-if="isAdmin"
                  size="sm"
                  @click="editEvent(event)"
                ></q-btn>
                {{ event.name }}</span
              ><span class="col text-right">{{
                formatStartEndTime(event)
              }}</span></q-card-section
            >
          </q-card>

          <q-card
            v-for="resource in event.resources"
            :key="resource.id"
            :class="resourceClasses(timestamp, resource)"
            flat
          >
            <q-card-section class="q-py-sm text-bold row"
              ><span
                class="col"
                style="overflow: hidden; text-overflow: ellipsis"
                >{{ resource.resourceType.name }}
                <q-icon
                  color="negative"
                  name="warning"
                  v-if="!(resource.minimumStaff <= resource.shifts.length)"
                ></q-icon></span
              ><span class="col text-right">{{
                formatStartEndTime(resource)
              }}</span></q-card-section
            >
            <q-separator> </q-separator>
            <q-list role="list" separator>
              <q-item v-for="shift in createUserList(resource)" :key="shift.id">
                <q-item-section
                  v-if="
                    isAdmin ||
                    (isTrainer(resource.resourceType) && isTaken(shift))
                  "
                  avatar
                >
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
                  <q-item-label overline v-if="isVacant(shift)"
                    >Ledig</q-item-label
                  >
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
                  <q-btn
                    flat
                    round
                    icon="edit"
                    title="Endre vakt"
                    v-if="showEditButton(timestamp, shift)"
                    @click="edit(shift, resource)"
                  ></q-btn>
                  <q-btn
                    flat
                    round
                    :aria-label="`Ring til ${shift?.user?.fullName}`"
                    icon="call"
                    type="a"
                    :href="'tel:' + shift?.user?.phoneNumber"
                    v-if="showCallButton(shift)"
                  ></q-btn>
                  <q-btn
                    flat
                    round
                    icon="add"
                    :disable="adding"
                    title="Ta vakt"
                    v-if="showAddButton(timestamp, shift)"
                    @click="checkTraining(resource)"
                  ></q-btn>
                </q-item-section>
              </q-item>
              <q-item v-if="showAddAdditionalRow(timestamp, resource)">
                <q-item-section> </q-item-section>
                <q-item-section side>
                  <q-btn
                    dense
                    flat
                    round
                    icon="add"
                    title="Ta vakt"
                    @click="addUserAsResource(resource)"
                  ></q-btn>
                </q-item-section>
              </q-item> </q-list></q-card
        ></template>
      </template>
    </q-calendar-agenda>
    <span id="dummy"></span>
    <q-menu v-model="showingMenu" :target="dateElement" auto-close>
      <q-list role="list">
        <q-item-label header>
          Velg mal for {{ formattedSelectedDay }}
        </q-item-label>
        <q-item
          v-for="template in templates"
          :key="template.id"
          clickable
          @click="applyTemplate(template.id)"
        >
          <q-item-section>
            <q-item-label>{{ template.name }}</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
    </q-menu>
    <q-dialog v-model="showingEventForm" persistent maximized>
      <EventForm
        :id="selectedEventId"
        :date="selectedDay"
        @cancel="showingEventForm = false"
        @saved="onEventSaved"
        @deleted="onEventSaved"
      ></EventForm>
    </q-dialog>
    <q-dialog v-model="showingEdit" persistent>
      <q-card class="full-width">
        <q-card-section class="text-h6 row"
          ><span> Redigere</span><q-space></q-space
          ><q-btn
            size="md"
            flat
            dense
            round
            icon="delete"
            color="negative"
            @click="deleteShift"
          ></q-btn>
        </q-card-section>
        <q-card-section class="text-center" v-if="isMissingTrainingInfo()"
          >Vi har ikke registrert at du har fått opplæring til denne type
          vakter, trenger du det?</q-card-section
        >
        <q-card-section
          class="row text-center q-gutter-sm"
          v-if="isMissingTrainingInfo()"
          ><div class="col-12">
            <q-btn @click="setTraining(true)" unelevated color="primary" no-caps
              >Yes! Jeg trenger opplæring! 🙋‍♂️</q-btn
            >
          </div>
          <div class="col-12">
            <q-btn flat no-caps @click="setTraining(false)"
              >Allerede fått opplæring, full kontroll! ✌️</q-btn
            >
          </div>

          <q-inner-loading :showing="savingTraining">
            <q-spinner size="3em" color="primary"></q-spinner> </q-inner-loading
        ></q-card-section>
        <q-card-section class="row q-gutter-sm">
          <q-input
            class="col-12"
            autofocus
            outlined
            label="Kommentar"
            v-model="selectedShift.comment"
          ></q-input>
          <div
            class="col-12 row items-right items-center"
            v-if="!isMissingTrainingInfo()"
          >
            <div class="q-mr-md">Fått opplæring</div>
            <q-btn-toggle
              disable
              v-model="selectedUserTraining.trainingComplete"
              toggle-color="primary"
              :options="[
                { label: 'Ja', value: true },
                { label: 'Nei', value: false },
              ]"
            /></div
        ></q-card-section>
        <q-card-actions align="right">
          <q-btn
            flat
            label="Avbryt"
            no-caps
            @click="showingEdit = false"
          ></q-btn>
          <q-btn
            unelevated
            color="primary"
            label="Lagre"
            no-caps
            @click="updateShift"
          ></q-btn> </q-card-actions
        ><q-inner-loading :showing="saving">
          <q-spinner size="3em" color="primary"></q-spinner>
        </q-inner-loading>
      </q-card>
    </q-dialog>
    <q-dialog v-model="showingAdminEdit" persistent>
      <q-card class="full-width">
        <q-card-section class="text-h6 row"
          ><span> Endre vakt</span><q-space></q-space
          ><q-btn
            :disable="!isAdmin"
            size="md"
            flat
            dense
            round
            icon="delete"
            color="negative"
            @click="deleteShift"
            v-if="selectedShift.id"
          ></q-btn>
        </q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-select
            :disable="!isAdmin"
            label="Navn"
            autofocus
            outlined
            :options="users"
            :loading="loadingUsers"
            option-label="fullName"
            option-value="id"
            v-model="selectedShift.user"
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
            :disable="!isAdmin"
            outlined
            label="Kommentar"
            v-model="selectedShift.comment"
          ></q-input>
          <div
            class="row items-right items-center"
            v-if="selectedResource.resourceType?.hasTraining"
          >
            <div class="q-mr-md">Fått opplæring</div>
            <q-btn-toggle
              v-model="selectedUserTraining.trainingComplete"
              :disable="!selectedShift.user"
              toggle-color="primary"
              :options="[
                { label: 'Ja', value: true },
                { label: 'Nei', value: false },
              ]"
            /></div
        ></q-card-section>
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
            @click="updateShift"
            :disable="!selectedShift.user"
          ></q-btn> </q-card-actions
        ><q-inner-loading :showing="saving">
          <q-spinner size="3em" color="primary"></q-spinner>
        </q-inner-loading>
      </q-card>
    </q-dialog>
    <q-dialog v-model="showingHallOfFame">
      <HallOfFameList
        class="full-width full-height"
        @close="showingHallOfFame = false"
        :currentUser="currentUser"
      ></HallOfFameList>
    </q-dialog>
    <q-dialog v-model="showingTrainingDialog" persistent>
      <q-card class="text-center">
        <q-card-section
          >Vi har ikke registrert at du har fått opplæring til denne type
          vakter, trenger du det?</q-card-section
        >
        <q-card-section class="q-gutter-md"
          ><q-btn
            @click="addUserAsResourceWithTraining"
            unelevated
            color="primary"
            no-caps
            >Yes! Jeg trenger opplæring! 🙋‍♂️</q-btn
          ></q-card-section
        >
        <q-card-section
          ><q-btn flat no-caps @click="addUserAsResourceWithoutTraining"
            >Allerede fått opplæring, full kontroll! ✌️</q-btn
          ></q-card-section
        >
      </q-card>
    </q-dialog>
    <q-footer>
      <q-toolbar>
        <q-btn
          title="Gå til forrige"
          no-caps
          flat
          :round="$q.platform.is.mobile"
          class="q-mr-xs"
          icon="navigate_before"
          :label="$q.platform.is.mobile ? undefined : 'Forrige'"
          @click="onPrev"
        ></q-btn>
        <q-btn
          title="Gå til idag"
          no-caps
          flat
          :round="$q.platform.is.mobile"
          class="q-mr-sm"
          icon="today"
          :label="$q.platform.is.mobile ? undefined : 'I dag'"
          @click="onToday"
        ></q-btn>
        <q-btn
          title="Vis kalender"
          no-caps
          class="q-mr-sm"
          flat
          :dense="$q.platform.is.mobile"
          icon="calendar_month"
          icon-right="arrow_drop_up"
        >
          <q-popup-proxy
            ref="qDateProxy"
            transition-show="scale"
            transition-hide="scale"
          >
            <q-date
              :first-day-of-week="1"
              :model-value="selectedDay"
              @update:model-value="setNow"
              @blur="(evt) => emit('blur', evt)"
              mask="YYYY-MM-DD"
              today-btn
              no-unset
              :events="eventStatusDates"
              :event-color="getEventColor"
              @navigation="getEventStatuses"
            >
              <div class="row items-center justify-end">
                <q-btn v-close-popup label="Lukk" color="primary" flat></q-btn>
              </div>
            </q-date>
          </q-popup-proxy>
        </q-btn>
        <q-btn
          v-if="$q.platform.is.mobile"
          no-caps
          flat
          round
          icon="navigate_next"
          @click="onNext"
          title="Gå til neste"
        ></q-btn>
        <!-- Blei dobbel knapp pga problem med icon-right-->
        <q-btn
          v-if="!$q.platform.is.mobile"
          flat
          no-caps
          icon-right="navigate_next"
          label="Neste"
          @click="onNext"
          title="Gå til neste"
        ></q-btn
        ><q-space></q-space>
        <q-btn
          title="Opprett ny vaktliste"
          v-if="isAdmin"
          fab
          unelevated
          padding="md"
          class="q-ma-xs"
          icon="add"
          color="accent"
          text-color="blue-grey-9"
          @click="addEvent"
      /></q-toolbar>
    </q-footer>

    <q-inner-loading :showing="adding">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-page>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { QCalendarAgenda, today } from "@quasar/quasar-ui-qcalendar";
import { parseISO, format, isValid, parse } from "date-fns";
import { useRouter } from "vue-router";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";
import { useAuthStore } from "stores/AuthStore";
import EventForm from "components/EventForm.vue";
import HallOfFameList from "components/HallOfFameList.vue";

const emit = defineEmits(["toggle-left", "toggle-right"]);
const props = defineProps({
  date: { type: String, required: true },
});

const loading = ref(false);
const selectedDay = ref(today());

const $q = useQuasar();
const $router = useRouter();
const mode = computed(() => {
  return $q.platform.is.mobile ? "day" : "week";
});
const isAdmin = computed(() => authStore.isAdmin);

const currentUser = computed(() => authStore.user);
const currentUserTrainings = computed(() =>
  currentUser.value.trainings.map((t) => t.resourceTypeId)
);
const eventStore = useEventStore();
const authStore = useAuthStore();
const userStore = useUserStore();

const showingEventForm = ref(false);

onMounted(async () => {
  userStore.getUser();
  if (isAdmin.value) eventStore.getTemplates();
  if (isValid(new Date(props.date))) selectedDay.value = props.date;
  else await $router.replace(`/day/${today()}`);
  const date = parse(selectedDay.value, "yyyy-MM-dd", new Date());
  const month = date.getMonth() + 1;
  const year = date.getFullYear();
  eventStore.getEventStatuses(month, year);
});

const calendar = ref(null);

async function onToday() {
  await calendar.value.moveToToday();
  $router.replace(`/day/${selectedDay.value}`);
}
async function onPrev() {
  await calendar.value.prev();
  $router.replace(`/day/${selectedDay.value}`);
}
async function onNext() {
  await calendar.value.next();
  $router.replace(`/day/${selectedDay.value}`);
}

async function onChange(event) {
  try {
    loading.value = true;
    await eventStore.getEventsForDates(event.start, event.end);
  } catch (error) {
    $q.notify({ message: "Klarte ikke å hente data, prøv å oppdatere siden." });
  } finally {
    loading.value = false;
  }
}

async function handleSwipe({ evt, ...info }) {
  if (info.direction === "right") await onPrev();
  if (info.direction === "left") await onNext();
}

function getEventsForDate(timestamp) {
  return eventStore.getEventsForDate(timestamp);
}

async function getEventStatuses(view) {
  await eventStore.getEventStatuses(view.month, view.year);
}

const eventStatusDates = computed(() => eventStore.eventStatusDates);

function getEventColor(date) {
  const dateString = format(
    parse(date, "yyyy/MM/dd", new Date()),
    "yyyy-MM-dd"
  );
  if (dateString < today()) return "grey-5";
  const status = eventStore.eventStatuses[date];
  return status ? "red-4" : "green-4";
}

function setNow(value) {
  selectedDay.value = value;
  $router.replace(`/day/${selectedDay.value}`);
}

function createUserList(resource) {
  const list = [];
  list.push(...resource.shifts);
  const neededStaff = resource.minimumStaff - list.length;

  if (neededStaff > 0) {
    for (let i = 0; i < neededStaff; i += 1) {
      list.push({
        id: 0,
        user: null,
        comment: null,
      });
    }
  }
  return list;
}

function formatTime(isoDateTime) {
  if (!isoDateTime) return null;
  const date = parseISO(isoDateTime);
  return format(date, "HH:mm");
}

function formatStartEndTime(event) {
  return `${formatTime(event.startTime)}-${formatTime(event.endTime)}`;
}

function resourceClasses(timestamp, resource) {
  return (
    "q-mt-sm q-mx-sm " +
    (timestamp.date < today()
      ? "bg-grey-3"
      : resource.minimumStaff <= resource.shifts.length
      ? "bg-green-1"
      : "bg-red-1")
  );
}

function isVacant(shift) {
  return (shift?.user?.id ?? 0) === 0 ?? false;
}

function isTaken(shift) {
  return shift?.user?.id > 0 ?? false;
}

function showEditButton(timestamp, shift) {
  return (
    timestamp.date >= today() &&
    (shift?.user?.id ?? 0) === currentUser.value?.id
  );
}

function showCallButton(shift) {
  return (
    (shift?.user?.id ?? 0) > 0 &&
    (shift?.user?.id ?? 0) !== currentUser.value?.id
  );
}

function showAddButton(timestamp, shift) {
  return timestamp.date >= today() && (shift?.user?.id ?? 0) === 0;
}

function showAddAdditionalRow(timestamp, resource) {
  return (
    (isTrainer(resource.resourceType) || isAdmin.value) &&
    timestamp.date >= today() &&
    resource.shifts.length >= resource.minimumStaff
  );
}

const adding = ref(false);
const showingTrainingDialog = ref(false);
async function checkTraining(resource) {
  try {
    adding.value = true;
    selectedResource.value = resource;
    if (isMissingTrainingInfo(resource)) {
      showingTrainingDialog.value = true;
    } else {
      const resourceType =
        resource?.resourceType ?? selectedResource.value?.resourceType;
      if (resourceType.hasTraining) {
        const training = currentUser.value?.trainings?.find(
          (t) => t.resourceTypeId === resourceType.id
        );
        await addUserAsResource(resource, training);
      } else {
        await addUserAsResource(resource);
      }
    }
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Oh no! Noe tryna da du skulle ta vakta! 🙈",
    });
  } finally {
    adding.value = false;
  }
}

function isMissingTrainingInfo(resource) {
  const resourceType =
    resource?.resourceType ?? selectedResource.value?.resourceType;
  return (
    resourceType.hasTraining &&
    !currentUserTrainings.value.includes(resourceType.id)
  );
}

const savingTraining = ref(false);
async function setTraining(needTraining) {
  try {
    savingTraining.value = true;
    await eventStore.addTraining(
      selectedResource.value,
      currentUser.value,
      needTraining
    );
  } catch (error) {
  } finally {
    savingTraining.value = false;
  }
}

function onUserUpdated() {
  selectedShift.value.userId = selectedShift.value?.user?.id ?? 0;
  const resourceTypeId = selectedResource.value?.resourceType.id;
  const training = selectedShift.value?.user.trainings.find(
    (t) => t.resourceTypeId === resourceTypeId
  );
  selectedUserTraining.value = training ?? {
    id: 0,
    resourceTypeId: resourceTypeId,
    userId: selectedShift.value.userId,
    trainingComplete: null,
  };
}

async function addUserAsResourceWithTraining() {
  showingTrainingDialog.value = false;
  let training = emptyUserTraining();
  training.trainingComplete = false;
  await addUserAsResource(selectedResource.value, training);
}

async function addUserAsResourceWithoutTraining() {
  showingTrainingDialog.value = false;
  let training = emptyUserTraining();
  training.trainingComplete = true;
  await addUserAsResource(selectedResource.value, training);
}

async function addUserAsResource(resource, training) {
  try {
    adding.value = true;

    await eventStore.addShift(resource, currentUser.value, null, training);
    $q.notify({
      message: "Woohoo! Du har tatt en vakt 🎉",
    });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Oh no! Noe tryna da du skulle ta vakta! 🙈",
    });
  } finally {
    adding.value = false;
  }
}

const showingEdit = ref(false);
const selectedShift = ref(null);
const emptyUserTraining = () => {
  return {
    id: 0,
    resourceTypeId: 0,
    userId: 0,
    trainingComplete: null,
  };
};
const selectedUserTraining = ref(emptyUserTraining());
function edit(shift, resource) {
  selectedResource.value = resource;
  selectedShift.value = Object.assign({}, shift);
  const resourceTypeId = resource?.resourceType.id;
  const training =
    selectedShift.value?.user.trainings.find(
      (t) => t.resourceTypeId === resourceTypeId
    ) ?? emptyUserTraining();
  selectedUserTraining.value = training;
  showingEdit.value = true;
}

const saving = ref(false);
async function updateShift() {
  try {
    saving.value = true;
    console.log(selectedShift.value);
    if (isAdmin.value && selectedShift.value.id === 0) {
      await eventStore.addShift(
        selectedResource.value,
        selectedShift.value.user,
        selectedShift.value.comment,
        selectedUserTraining.value
      );
    } else {
      await eventStore.updateShift(
        selectedResource.value,
        selectedShift.value,
        selectedUserTraining.value
      );
    }

    selectedResource.value = null;
    selectedShift.value = null;
    selectedUserTraining.value = null;
    showingEdit.value = false;
    showingAdminEdit.value = false;
    $q.notify({
      message: "Endringer er lagret 👍",
    });
  } catch (error) {
    console.log(error);
    $q.notify({
      message: "Oh no! Noe tryna da vi skulle lagre endringene! 🙈",
    });
  } finally {
    saving.value = false;
  }
}

async function deleteShift() {
  try {
    saving.value = true;
    await eventStore.deleteShift(selectedShift.value);
    showingEdit.value = false;
    showingAdminEdit.value = false;
    $q.notify({
      message: "Ajaj! Du har tatt bort vakta 😱",
    });
  } catch (error) {
    $q.notify({
      message: "Oh no! Noe tryna da vi skulle ta bort vakta... 🙈",
    });
  } finally {
    saving.value = false;
  }
}

function isTrainer(resourceType) {
  const trainerIds = resourceType.trainers.map((t) => t.id);
  return trainerIds.includes(currentUser.value.id);
}

const selectedResource = ref(null);
const showingAdminEdit = ref(false);
async function editShift(resource, shift) {
  selectedShift.value = Object.assign({ userId: 0 }, shift);
  selectedResource.value = resource;
  const resourceTypeId = resource?.resourceType.id;
  const training =
    selectedShift.value?.user?.trainings.find(
      (t) => t.resourceTypeId === resourceTypeId
    ) ?? emptyUserTraining();
  selectedUserTraining.value = training;
  showingAdminEdit.value = true;
  if (isAdmin.value) await getUsers();
}

const loadingUsers = ref(false);
const users = computed(() => userStore.users);
async function getUsers() {
  try {
    loadingUsers.value = true;
    await userStore.getUsers();
  } catch (error) {
    $q.notify({ message: "Klarte ikke å hente lista over brukere." });
  } finally {
    loadingUsers.value = false;
  }
}

function onEventSaved() {
  showingEventForm.value = false;
  calendar.value.updateCurrent();
  const date = formatISO(startDateTime.value, {
    representation: "date",
  });
  $router.push(`/day/${date}`);
}

const selectedEventId = ref(null);
function editEvent(event) {
  if (!isAdmin.value) return;
  selectedEventId.value = event.id;
  showingEventForm.value = true;
}

function addEvent() {
  if (!isAdmin.value) return;
  selectedEventId.value = null;
  showingEventForm.value = true;
}

const templates = computed(() => eventStore.templates);
const showingMenu = ref(false);
const formattedSelectedDay = computed(() =>
  selectedDay.value
    ? format(parse(selectedDay.value, "yyyy-MM-dd", new Date()), "dd.MM")
    : ""
);
const dateElement = ref("#dummy");
function showMenu(data) {
  if (!isAdmin.value || !templates.value.length) return;
  selectedDay.value = data?.scope?.timestamp?.date;
  dateElement.value = data?.event?.target;
  showingMenu.value = true;
}

async function applyTemplate(id) {
  try {
    loading.value = true;
    await eventStore.createEventFromTemplate(id, selectedDay.value);
    $q.notify({ message: "Vaktlista er lagt til." });
  } catch (error) {
  } finally {
    loading.value = false;
  }
}

const showingHallOfFame = ref(false);
async function showHallOfFame() {
  showingHallOfFame.value = true;
}
</script>
