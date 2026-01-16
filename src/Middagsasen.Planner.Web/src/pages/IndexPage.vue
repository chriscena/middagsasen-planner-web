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
        />
        <img
          src="~assets/middagsasen-banner-white.svg"
          class="q-ml-sm"
          style="max-height: 40px; max-width: 50vw"
        />
        <q-space></q-space>
        <q-btn
          title="Timeføring"
          dense
          flat
          round
          icon="more_time"
          @click="openTimetrackingForm"
        />
        <q-btn
          title="Din brukerinfo"
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
        />
      </q-toolbar> </q-header
    ><q-badge
      outline
      color="primary"
      class="q-ma-md"
      style="position: absolute; top: 0px; left: 0px; z-index: 1000"
      >Uke {{ weekNumber }}</q-badge
    >
    <q-calendar-agenda
      locale="no"
      :view="mode"
      v-model="selectedDay"
      date-type="rounded"
      :weekdays="[1, 2, 3, 4, 5, 6, 0]"
      @change="onChange"
      @click-head-day="showMenu"
      animated
      column-header-before
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
            <q-card-section class="q-py-sm text-bold">
              <div class="row">
                <span class="col"> {{ event.name }}</span
                ><span class="col text-right">{{
                  formatStartEndTime(event)
                }}</span>
              </div>
              <div class="row">
                <span class="col text-caption">{{ event.description }}</span>
                <span class="col text-right">
                  <q-btn
                    flat
                    round
                    title="Redigere vaktliste"
                    icon="edit"
                    v-if="isAdmin"
                    size="sm"
                    @click="editEvent(event)"
                  ></q-btn
                ></span>
              </div>
            </q-card-section>
          </q-card>

          <EventItemCard
            :model-value="event"
            :timestamp="timestamp"
            :is-admin="isAdmin"
          ></EventItemCard>
        </template>
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
    <q-dialog v-model="showingTimetrackingForm" persistent>
      <TimeTrackingForm
        @cancel="showingTimetrackingForm = false"
        @saved="showingTimetrackingForm = false"
      ></TimeTrackingForm>
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
  </q-page>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { QCalendarAgenda, today } from "@quasar/quasar-ui-qcalendar";
import { parseISO, format, isValid, parse } from "date-fns";
import { nb } from "date-fns/locale";
import { useRouter } from "vue-router";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";
import { useAuthStore } from "stores/AuthStore";
import EventItemCard from "components/EventItemCard.vue";
import EventForm from "components/EventForm.vue";
import TimeTrackingForm from "components/TimeTrackingForm.vue";

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

const weekNumber = computed(() => {
  const date = parseISO(props.date);
  return format(date, "w", { locale: nb });
});

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

function formatTime(isoDateTime) {
  if (!isoDateTime) return null;
  const date = parseISO(isoDateTime);
  return format(date, "HH:mm");
}

function formatStartEndTime(event) {
  return `${formatTime(event.startTime)}-${formatTime(event.endTime)}`;
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

const showingTimetrackingForm = ref(false);
function openTimetrackingForm() {
  showingTimetrackingForm.value = true;
}
</script>
