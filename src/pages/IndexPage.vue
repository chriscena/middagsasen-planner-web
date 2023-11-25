<template>
  <q-page padding>
    <q-header>
      <q-toolbar>
        <q-btn flat round icon="menu" @click="emit('toggle-left')"></q-btn>
        <q-space></q-space>
        <q-btn flat round icon="person" @click="emit('toggle-right')"></q-btn>
      </q-toolbar>
    </q-header>
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
          <q-card class="q-mt-sm q-mx-sm" flat bordered>
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
              'q-mt-sm q-mx-sm ' +
              (resource.minimumStaff <= resource.users.length
                ? 'bg-green-1'
                : 'bg-red-1')
            "
            flat
          >
            <q-card-section class="q-py-sm text-bold row"
              ><span class="col"
                >{{ resource.resourceTypeName }}
                <q-icon
                  color="negative"
                  name="warning"
                  v-if="!(resource.minimumStaff <= resource.users.length)"
                ></q-icon></span
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
                    @click="edit(user)"
                  ></q-btn>
                  <q-btn flat round icon="call" v-if="user.userId > 1"></q-btn>
                  <q-btn
                    flat
                    round
                    icon="add"
                    v-if="user.userId === 0"
                    @click="addUserAsResource(resource.eventResourceId)"
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
                    @click="addUserAsResource(resource.eventResourceId)"
                  ></q-btn>
                </q-item-section>
              </q-item>
            </q-list> </q-card
        ></template>
      </template>
    </q-calendar-agenda>
    <q-dialog v-model="showingEdit" persistent>
      <q-card>
        <q-card-section class="text-h6 row"
          ><span> Redigere</span><q-space></q-space
          ><q-btn
            size="md"
            flat
            dense
            round
            icon="delete"
            color="negative"
            @click="deleteUser"
          ></q-btn>
        </q-card-section>
        <q-card-section>
          <q-input
            outlined
            label="Kommentar"
            v-model="selectedUser.comment"
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
            @click="saveUser"
          ></q-btn>
        </q-card-actions>
      </q-card>
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
          fab
          unelevated
          padding="md"
          class="q-ma-xs"
          icon="add"
          color="accent"
          @click="$router.push('/event')"
      /></q-toolbar>
    </q-footer>
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

const emit = defineEmits(["toggle-left", "toggle-right"]);

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

function addUserAsResource(eventResourceId) {
  eventStore.addUser(eventResourceId, currentUser.value);
}

const showingEdit = ref(false);
const selectedUser = ref(null);
function edit(user) {
  selectedUser.value = Object.assign({}, user);
  showingEdit.value = true;
}

function saveUser() {
  eventStore.updateUser(selectedUser.value);
  showingEdit.value = false;
}

function deleteUser() {
  eventStore.deleteUser(selectedUser.value);
  showingEdit.value = false;
}
</script>
