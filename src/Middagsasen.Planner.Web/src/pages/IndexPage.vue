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
          src="~@/assets/middagsasen-banner-white.svg"
          class="q-ml-sm"
          style="max-height: 40px; max-width: 50vw"
        />
        <q-space></q-space>
        <q-btn
          v-if="isAdmin"
          title="Hall of Fame"
          dense
          flat
          round
          icon="emoji_events"
          @click="openHallOfFame"
        />
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
                  formatTimeRange(event.startTime, event.endTime)
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
        @deleted="onEventDeleted"
      ></EventForm>
    </q-dialog>
    <q-dialog v-model="showingTimetrackingForm" persistent>
      <TimeTrackingForm
        @cancel="showingTimetrackingForm = false"
        @saved="showingTimetrackingForm = false"
      ></TimeTrackingForm>
    </q-dialog>
    <q-dialog v-model="showingHallOfFame">
      <HallOfFameList @close="showingHallOfFame = false"></HallOfFameList>
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
            @before-show="getEventStatuses"
          >
            <VueDatePicker
              inline
              :model-type="DAY_KEY_FORMAT"
              v-model="selectedDay"
              week-numbers
              auto-apply
              :locale="nb"
              :time-config="{ enableTimePicker: false }"
              :markers="markers"
              @update:model-value="setNow"
              @update-month-year="getEventStatuses"
            ></VueDatePicker>
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

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useQuasar } from "quasar";
import type { TouchSwipeValue } from "quasar";
// QCalendarAgenda brukes både som komponent (q-calendar-agenda) og som
// instanstype for ref-en (prev/next/moveToToday/updateCurrent).
import { QCalendarAgenda } from "@quasar/quasar-ui-qcalendar";
import type { Timestamp } from "@timestamp-js/core";
import { nb } from "date-fns/locale";
import { useRouter } from "vue-router";
import { useEventStore } from "@/stores/EventStore";
import { useUserStore } from "@/stores/UserStore";
import { useAuthStore } from "@/stores/AuthStore";
import EventItemCard from "@/components/EventItemCard.vue";
import EventForm from "@/components/EventForm.vue";
import TimeTrackingForm from "@/components/TimeTrackingForm.vue";
import HallOfFameList from "@/components/HallOfFameList.vue";
import type { EventRequest, EventResponse } from "@/types";
import {
  DAY_KEY_FORMAT,
  formatDayMonth,
  formatTimeRange,
  formatWeekNumber,
  fromDayKey,
  isDayKey,
  isPast,
  parseEventStatusDate,
  toDayKey,
  today,
} from "@/shared/time";
import { notifyApiError } from "@/shared/notifyApiError";

// Payload fra q-calendar-agenda sitt change-event. QCalendar 5 typer ikke
// emits-payloadene, så vi beskriver kun feltene vi bruker.
interface CalendarChangeEvent {
  start: string;
  end: string;
}

// Dagen fra q-calendar-agenda; kun date (yyyy-MM-dd) brukes.
type DayTimestamp = Pick<Timestamp, "date">;

// Payload fra click-head-day. target er dag-elementet menyen skal knyttes til.
interface HeadDayClickEvent {
  scope: { timestamp: DayTimestamp };
  event: { target: Element };
}

// Payload fra VueDatePicker sitt update-month-year (month er 0-basert).
// before-show fra q-popup-proxy sender i stedet et Event uten month/year.
interface MonthYear {
  month?: number | null;
  year?: number | null;
}

// Argumentet til v-touch-swipe-handleren.
type SwipeDetails = Parameters<
  Extract<TouchSwipeValue, (...args: never[]) => unknown>
>[0];

const emit = defineEmits<{
  "toggle-left": [];
  "toggle-right": [];
}>();
const props = defineProps<{
  date: string;
}>();

// Kalenderhenting (onChange) og vaktliste fra mal (applyTemplate) styrer hver
// sin del, så den ene ikke skjuler lasteindikatoren mens den andre pågår.
const calendarLoading = ref(false);
const applyingTemplate = ref(false);
const loading = computed(() => calendarLoading.value || applyingTemplate.value);
// Initialiseres direkte fra URL-en, slik at kalenderen starter på riktig uke
// og ikke først sender change for dagens uke.
const selectedDay = ref(isDayKey(props.date) ? props.date : today());

const $q = useQuasar();
const $router = useRouter();

const mode = computed(() => {
  return $q.platform.is.mobile ? "day" : "week";
});
const isAdmin = computed(() => authStore.isAdmin);

const eventStore = useEventStore();
const authStore = useAuthStore();
const userStore = useUserStore();

const weekNumber = computed(() => {
  // selectedDay er alltid en gyldig dato, i motsetning til props.date.
  return formatWeekNumber(selectedDay.value);
});

const showingEventForm = ref(false);
const showingHallOfFame = ref(false);

onMounted(async () => {
  userStore.getUser();
  if (isAdmin.value) eventStore.getTemplates();
  if (!isDayKey(props.date)) await $router.replace(`/day/${today()}`);
  const date = fromDayKey(selectedDay.value);
  const month = date.getMonth() + 1;
  const year = date.getFullYear();
  eventStore.getEventStatuses(month, year);
});

const calendar = ref<QCalendarAgenda | null>(null);

// URL-en følger valgt dag, uansett hva som endret den (prev/next/i dag,
// datovelger, klikk i kalenderen, lagret vaktliste). Sammenligningen med
// props.date hindrer løkke når endringen kom fra URL-en (watch under).
watch(selectedDay, (day) => {
  if (day !== props.date) $router.replace(`/day/${day}`);
});

// Navigering i URL-en (tilbake/frem, manuell endring) mens siden er montert.
// Ugyldige datoer ignoreres; onMounted tar seg av dem ved oppstart.
watch(
  () => props.date,
  (date) => {
    if (isDayKey(date) && date !== selectedDay.value) selectedDay.value = date;
  }
);

// Kalenderen rendres alltid (ingen v-if), så ref-en er satt etter mount.
// Metodene oppdaterer v-model (selectedDay); watch-en over tar URL-en.
function onToday() {
  calendar.value!.moveToToday();
}
function onPrev() {
  calendar.value!.prev();
}
function onNext() {
  calendar.value!.next();
}

// Teller slik at kun siste forespørsel styrer calendarLoading og feilmelding
// (EventStore forkaster selv utdaterte svar).
let latestChange = 0;
async function onChange(event: CalendarChangeEvent) {
  const request = ++latestChange;
  try {
    calendarLoading.value = true;
    await eventStore.getEventsForDates(event.start, event.end);
  } catch (error) {
    if (request === latestChange)
      notifyApiError(
        error,
        "Klarte ikke å hente data, prøv å oppdatere siden."
      );
  } finally {
    if (request === latestChange) calendarLoading.value = false;
  }
}

function handleSwipe(info: SwipeDetails) {
  if (info.direction === "right") onPrev();
  if (info.direction === "left") onNext();
}

function getEventsForDate(timestamp: DayTimestamp) {
  return eventStore.getEventsForDate(timestamp);
}

async function getEventStatuses(eventOrView?: MonthYear | Event) {
  // Et Event har verken year eller month, så det faller til selectedDay under.
  const view = eventOrView as MonthYear | undefined;
  if (
    !view ||
    view.year === undefined ||
    view.year === null ||
    view.month === undefined ||
    view.month === null
  )
    await eventStore.getEventStatuses(
      fromDayKey(selectedDay.value).getMonth() + 1,
      fromDayKey(selectedDay.value).getFullYear()
    );
  else await eventStore.getEventStatuses(view.month + 1, view.year);
}

// date er på formen fra /api/eventstatus (yyyy/MM/dd).
function getEventColor(date: string) {
  if (isPast(parseEventStatusDate(date))) return "#bdbdbd"; // grey-5
  const status = eventStore.eventStatuses[date];
  return status ? "#e57373" /* red-4 */ : "#81c784"; /* green-4 */
}

const markers = computed(() => {
  return eventStore.eventStatusDates.map((date) => {
    return {
      date: date,
      type: "dot",
      color: getEventColor(date),
    };
  });
});

// URL-en oppdateres av watch(selectedDay).
function setNow(value: string) {
  selectedDay.value = value;
}

// EventForm sender EventRequest ved lagring (startTime er "yyyy-MM-ddTHH:mm"
// i lokal tid); kalenderen går til dagen for vaktlista.
function onEventSaved(model: EventRequest) {
  showingEventForm.value = false;
  selectedDay.value = toDayKey(model.startTime);
  calendar.value!.updateCurrent(); // satt etter mount, se onToday
  // URL-en oppdateres av watch(selectedDay).
}

// Etter sletting blir kalenderen stående på valgt dag.
function onEventDeleted() {
  showingEventForm.value = false;
  calendar.value!.updateCurrent(); // satt etter mount, se onToday
}

const selectedEventId = ref<number | null>(null);
function editEvent(event: EventResponse) {
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
const formattedSelectedDay = computed(() => formatDayMonth(selectedDay.value));
const dateElement = ref<string | Element>("#dummy");
function showMenu(data: HeadDayClickEvent) {
  if (!isAdmin.value || !templates.value.length) return;
  selectedDay.value = data?.scope?.timestamp?.date;
  dateElement.value = data?.event?.target;
  showingMenu.value = true;
}

async function applyTemplate(id: number) {
  // Et raskt dobbeltklikk skal ikke lage to vaktlister.
  if (applyingTemplate.value) return;
  try {
    applyingTemplate.value = true;
    await eventStore.createEventFromTemplate(id, selectedDay.value);
    $q.notify({ message: "Vaktlista er lagt til." });
  } catch (error) {
    notifyApiError(error, "Klarte ikke å legge til vaktlista fra malen.");
  } finally {
    applyingTemplate.value = false;
  }
}

function openHallOfFame() {
  showingHallOfFame.value = true;
}

const showingTimetrackingForm = ref(false);
function openTimetrackingForm() {
  showingTimetrackingForm.value = true;
}
</script>
