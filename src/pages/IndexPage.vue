<template>
  <q-page padding>
    <div v-if="$q.platform.is.desktop" class="text-center q-mb-md">
      <q-btn-group>
        <q-btn
          icon="navigate_before"
          :label="$q.platform.is.mobile ? undefined : 'Forrige'"
          @click="onPrev"
        ></q-btn>
        <q-btn
          icon="today"
          :label="$q.platform.is.mobile ? undefined : 'I dag'"
          @click="onToday"
        ></q-btn>
        <q-btn icon="calendar_month" icon-right="arrow_drop_up">
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
          icon-right="navigate_next"
          :label="$q.platform.is.mobile ? undefined : 'Neste'"
          @click="onNext"
        ></q-btn>
      </q-btn-group>
    </div>
    <div>
      <q-calendar-agenda
        locale="no"
        :view="mode"
        v-model="selectedDay"
        :weekdays="[1, 2, 3, 4, 5, 6, 7]"
        @change="onChange"
        animated
        @click-date="dateClicked"
        ref="calendar"
      >
        <template #head-days-events>
          <q-linear-progress
            size="xs"
            v-show="loading"
            indeterminate
          ></q-linear-progress>
        </template>
        <!-- <template #day="{ scope: { timestamp } }"> -->
        <template #day>
          <template v-for="event in events" :key="event.eventId">
            <q-card class="q-mt-sm" flat bordered>
              <q-card-section class="q-py-sm text-bold row">
                <span class="col">{{ event.eventName }}</span
                ><span class="col text-right">{{
                  formatStartEndTime(event)
                }}</span></q-card-section
              >
            </q-card>

            <q-card
              v-for="resource in event.resources"
              :key="resource.eventResourceId"
              :class="
                'q-mt-sm ' +
                (resource.minimumStaff <= resource.users.length
                  ? 'bg-green-1'
                  : 'bg-red-1')
              "
              flat
            >
              <q-card-section class="q-py-sm text-bold row"
                ><span class="col">{{ resource.resourceTypeName }}</span
                ><span class="col text-right">{{
                  formatStartEndTime(resource)
                }}</span></q-card-section
              >
              <q-separator> </q-separator>
              <q-list separator>
                <q-item
                  v-for="user in createUserList(resource)"
                  :key="user.eventResourceUserId"
                >
                  <q-item-section>
                    <q-item-label overline v-if="user.userId === 0"
                      >Ledig</q-item-label
                    >
                    <q-item-label v-if="user.userId > 0">{{
                      user.name
                    }}</q-item-label>
                    <q-item-label caption v-if="user.userId > 0">{{
                      user.comment
                    }}</q-item-label>
                  </q-item-section>
                  <q-item-section side>
                    <q-btn
                      flat
                      round
                      icon="edit"
                      v-if="user.userId === currentUser.userId"
                    ></q-btn>
                    <q-btn
                      flat
                      round
                      icon="call"
                      v-if="user.userId > 1"
                    ></q-btn>
                    <q-btn
                      flat
                      round
                      icon="add"
                      v-if="user.userId === 0"
                    ></q-btn>
                  </q-item-section>
                </q-item>
                <q-item v-if="resource.users.length >= resource.minimumStaff">
                  <q-item-section> </q-item-section>
                  <q-item-section side>
                    <q-btn
                      dense
                      flat
                      round
                      icon="add"
                      @click="addUserAsResource(user)"
                    ></q-btn>
                  </q-item-section>
                </q-item>
              </q-list> </q-card
          ></template>
        </template>
      </q-calendar-agenda>
    </div>
    <q-footer v-if="$q.platform.is.mobile" class="text-center" elevated>
      <q-btn-group flat>
        <q-btn
          icon="navigate_before"
          :label="$q.platform.is.mobile ? undefined : 'Forrige'"
          @click="onPrev"
        ></q-btn>
        <q-btn
          icon="today"
          :label="$q.platform.is.mobile ? undefined : 'I dag'"
          @click="onToday"
        ></q-btn>
        <q-btn icon="calendar_month" icon-right="arrow_drop_up">
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
          icon-right="navigate_next"
          :label="$q.platform.is.mobile ? undefined : 'Neste'"
          @click="onNext"
        ></q-btn>
      </q-btn-group>
    </q-footer>
    <q-page-sticky position="bottom-right" :offset="[18, 18]">
      <q-btn fab icon="add" color="accent" @click="$router.push('/event')" />
    </q-page-sticky>
  </q-page>
</template>

<script setup>
import { computed, inject, onMounted, ref, reactive } from "vue";
import { useQuasar } from "quasar";
import {
  QCalendarAgenda,
  today,
  createNativeLocaleFormatter,
  parseTimestamp,
} from "@quasar/quasar-ui-qcalendar";
import {
  parseISO,
  format,
  formatISO,
  addDays,
  isBefore,
  isAfter,
} from "date-fns";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";

const loading = false;
const selectedDay = ref(today());
const $q = useQuasar();
const $router = useRouter();
const mode = computed(() => {
  return $q.platform.is.mobile ? "day" : "week";
});

const events = computed(() => eventStore.events);
const currentUser = computed(() => userStore.user);

const eventStore = useEventStore();
const userStore = useUserStore();
onMounted(() => {
  userStore.getUser();
  eventStore.getEvents();
});

const calendar = ref(null);

function onToday() {
  calendar.value.moveToToday();
}
function onPrev() {
  calendar.value.prev();
}
function onNext() {
  calendar.value.next();
}
function dateClicked() {}
function onChange() {}

function setNow(value) {
  selectedDay.value = value;
}

function createUserList(resource) {
  const list = [];
  list.push(...resource.users);
  const neededStaff = resource.minimumStaff - list.length;

  if (neededStaff > 0) {
    for (let i = 0; i < neededStaff; i += 1) {
      list.push({
        eventResourceUserId: 0,
        userId: 0,
        name: null,
        phoneNumber: null,
        comment: null,
      });
    }
  }
  return list;
}

function formatTime(isoDateTime) {
  const date = parseISO(isoDateTime);
  return format(date, "HH:mm");
}

function formatStartEndTime(event) {
  return `${formatTime(event.startTime)}-${formatTime(event.endTime)}`;
}

function addUserAsResource(user) {
  Object.assign(user, currentUser.value);
}
</script>
