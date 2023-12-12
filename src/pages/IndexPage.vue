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
        ></q-btn>
        <q-space></q-space>
        <q-btn
          class="q-mr-sm"
          dense
          flat
          round
          icon="emoji_events"
          @click="showHallOfFame"
          color="amber"
          v-if="isAdmin"
        ></q-btn>
        <q-btn
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
      id="calendar"
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
            :class="
              'q-mt-sm q-mx-sm ' +
              (resource.minimumStaff <= resource.shifts.length
                ? 'bg-green-1'
                : 'bg-red-1')
            "
            flat
          >
            <q-card-section class="q-py-sm text-bold row"
              ><span class="col"
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
            <q-list separator>
              <q-item v-for="shift in createUserList(resource)" :key="shift.id">
                <q-item-section v-if="isAdmin" avatar>
                  <q-btn
                    flat
                    round
                    icon="edit"
                    size="sm"
                    @click="editShift(resource, shift)"
                  ></q-btn>
                </q-item-section>
                <q-item-section>
                  <q-item-label
                    overline
                    v-if="(shift?.user?.id ?? 0) === 0 ?? false"
                    >Ledig</q-item-label
                  >
                  <q-item-label v-if="shift?.user?.id > 0">{{
                    shift.user.fullName
                  }}</q-item-label>
                  <q-item-label caption v-if="shift?.user?.id > 0">{{
                    shift.comment
                  }}</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <q-btn
                    flat
                    round
                    icon="edit"
                    v-if="
                      timestamp.date >= today() &&
                      (shift?.user?.id ?? 0) === currentUser?.id
                    "
                    @click="edit(shift)"
                  ></q-btn>
                  <q-btn
                    flat
                    round
                    icon="call"
                    type="a"
                    :href="'tel:' + shift?.user?.phoneNumber"
                    v-if="
                      (shift?.user?.id ?? 0) > 1 &&
                      (shift?.user?.id ?? 0) !== currentUser?.id
                    "
                  ></q-btn>
                  <q-btn
                    flat
                    round
                    icon="add"
                    :disable="adding"
                    v-if="
                      timestamp.date >= today() && (shift?.user?.id ?? 0) === 0
                    "
                    @click="addUserAsResource(resource)"
                  ></q-btn>
                </q-item-section>
              </q-item>
              <q-item
                v-if="
                  timestamp.date >= today() &&
                  resource.shifts.length >= resource.minimumStaff
                "
              >
                <q-item-section> </q-item-section>
                <q-item-section side>
                  <q-btn
                    dense
                    flat
                    round
                    icon="add"
                    @click="addUserAsResource(resource)"
                  ></q-btn>
                </q-item-section>
              </q-item> </q-list></q-card
        ></template>
      </template>
    </q-calendar-agenda>
    <q-menu v-model="showingMenu" :target="dateElement" auto-close>
      <q-list>
        <q-item-label header>
          Velg mal for {{ formattedDateContext }}
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
        <q-card-section>
          <q-input
            autofocus
            outlined
            label="Kommentar"
            v-model="selectedShift.comment"
          ></q-input>
        </q-card-section>
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
            label="Navn"
            autofocus
            outlined
            :options="users"
            :loading="loadingUsers"
            option-label="fullName"
            option-value="id"
            v-model="selectedShift.user"
            @update:model-value="
              selectedShift.userId = selectedShift?.user?.id ?? 0
            "
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
            v-model="selectedShift.comment"
          ></q-input>
        </q-card-section>
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
    <q-footer>
      <q-toolbar>
        <q-btn
          no-caps
          flat
          :round="$q.platform.is.mobile"
          class="q-mr-xs"
          icon="navigate_before"
          :label="$q.platform.is.mobile ? undefined : 'Forrige'"
          @click="onPrev"
        ></q-btn>
        <q-btn
          no-caps
          flat
          :round="$q.platform.is.mobile"
          class="q-mr-sm"
          icon="today"
          :label="$q.platform.is.mobile ? undefined : 'I dag'"
          @click="onToday"
        ></q-btn>
        <q-btn
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
              event-color="green"
              today-btn
              no-unset
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
        ></q-btn>
        <!-- Blei dobbel knapp pga problem med icon-right-->
        <q-btn
          v-if="!$q.platform.is.mobile"
          flat
          no-caps
          icon-right="navigate_next"
          label="Neste"
          @click="onNext"
        ></q-btn
        ><q-space></q-space>
        <q-btn
          v-if="isAdmin"
          fab
          unelevated
          padding="md"
          class="q-ma-xs"
          icon="add"
          color="accent"
          text-color="blue-grey-9"
          @click="toCreate()"
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
const eventStore = useEventStore();
const authStore = useAuthStore();
const userStore = useUserStore();
onMounted(async () => {
  userStore.getUser();
  if (isAdmin.value) eventStore.getTemplates();
  if (isValid(new Date(props.date))) selectedDay.value = props.date;
  else await $router.replace(`/day/${today()}`);
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

function getEventsForDate(timestamp) {
  return eventStore.getEventsForDate(timestamp);
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

const adding = ref(false);
async function addUserAsResource(resource) {
  try {
    adding.value = true;
    await eventStore.addShift(resource, currentUser.value);
    $q.notify({
      message: "Woohoo! Du har tatt en vakt 🎉",
    });
  } catch (error) {
    $q.notify({
      message: "Oh no! Noe tryna da du skulle ta vakta! 🙈",
    });
  } finally {
    adding.value = false;
  }
}

const showingEdit = ref(false);
const selectedShift = ref(null);
function edit(shift) {
  selectedShift.value = Object.assign({}, shift);
  showingEdit.value = true;
}

const saving = ref(false);
async function updateShift() {
  try {
    saving.value = true;
    if (isAdmin.value && selectedShift.value.id === 0) {
      await eventStore.addShift(
        selectedResource.value,
        selectedShift.value.user
      );
    } else {
      await eventStore.updateShift(selectedShift.value);
    }
    showingEdit.value = false;
    showingAdminEdit.value = false;
    $q.notify({
      message: "Endringer er lagret 👍",
    });
  } catch (error) {
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

const selectedResource = ref(null);
const showingAdminEdit = ref(false);
async function editShift(resource, shift) {
  selectedShift.value = Object.assign({ userId: 0 }, shift);
  selectedResource.value = resource;
  showingAdminEdit.value = true;
  await getUsers();
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

function toCreate() {
  $router.push(`/create/${selectedDay.value}`);
}

function editEvent(event) {
  $router.push(`/edit/${event.id}`);
}

const templates = computed(() => eventStore.templates);
const showingMenu = ref(false);
const dateContext = ref(null);
const formattedDateContext = computed(() =>
  dateContext.value
    ? format(parse(dateContext.value, "yyyy-MM-dd", new Date()), "dd.MM")
    : ""
);
const dateElement = ref("#calendar");
function showMenu(data) {
  if (!isAdmin.value || !templates.value.length) return;
  dateContext.value = data?.scope?.timestamp?.date;
  dateElement.value = data?.event?.target;
  showingMenu.value = true;
}

async function applyTemplate(id) {
  try {
    loading.value = true;
    await eventStore.createEventFromTemplate(id, dateContext.value);
    $q.notify({ message: "Vaktliste opprettet." });
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
