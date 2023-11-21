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
          <q-card class="q-ma-sm bg-green-1" flat>
            <q-card-section class="q-py-sm text-bold">Test</q-card-section>
            <q-separator> </q-separator>
            <q-list separator>
              <q-item>
                <q-item-section>
                  <q-item-label>Navn</q-item-label>
                  <q-item-label caption>Dette er min kommentar</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <q-btn flat round icon="edit"></q-btn>
                </q-item-section>
              </q-item>
              <q-item>
                <q-item-section>
                  <q-item-label>Navn</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <q-btn flat round icon="call"></q-btn>
                </q-item-section>
              </q-item>
            </q-list>
            <q-card-section>Test</q-card-section>
          </q-card>

          <q-card class="q-ma-sm bg-red-1" flat>
            <q-card-section>Test</q-card-section>
            <q-separator> </q-separator>
            <q-list separator>
              <q-item>
                <q-item-section>
                  <q-item-label overline>Ledig</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <q-btn flat round icon="add"></q-btn>
                </q-item-section>
              </q-item>
              <q-item>
                <q-item-section>
                  <q-item-label overline>Ledig</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <q-btn flat round icon="add"></q-btn>
                </q-item-section>
              </q-item>
            </q-list>
          </q-card>
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
import { formatISO, addDays, isBefore, isAfter } from "date-fns";
import { useI18n } from "vue-i18n";

const loading = false;
const selectedDay = ref(today());
const $q = useQuasar();

const mode = computed(() => {
  return $q.platform.is.mobile ? "day" : "week";
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
</script>
