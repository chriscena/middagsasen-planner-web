<template>
  <q-page padding>
    <q-header
      ><q-toolbar>
        <q-btn flat dense round icon="close" @click="$router.push('/')"></q-btn>
        <q-toolbar-title>Vaktliste</q-toolbar-title>
        <q-space></q-space>
        <q-btn color="accent" flat label="Lagre" no-caps></q-btn> </q-toolbar
    ></q-header>
    <div class="q-gutter-sm">
      <q-input outlined label="Navn" v-model="name"></q-input>
      <q-input outlined label="Dato" :model-value="date"
        ><template v-slot:append>
          <q-icon name="event" class="cursor-pointer">
            <q-popup-proxy
              cover
              transition-show="scale"
              transition-hide="scale"
            >
              <q-date v-model="startDateTime" mask="YYYY-MM-DDTHH:mm">
                <div class="row items-center justify-end">
                  <q-btn v-close-popup label="Close" color="primary" flat />
                </div>
              </q-date>
            </q-popup-proxy>
          </q-icon> </template
      ></q-input>
      <q-input outlined label="Start" :model-value="startTime">
        <template v-slot:append>
          <q-icon name="access_time" class="cursor-pointer">
            <q-popup-proxy
              cover
              transition-show="scale"
              transition-hide="scale"
            >
              <q-time v-model="startDateTime" format24h mask="YYYY-MM-DDTHH:mm">
                <div class="row items-center justify-end">
                  <q-btn v-close-popup label="Close" color="primary" flat />
                </div>
              </q-time>
            </q-popup-proxy>
          </q-icon> </template
      ></q-input>
      <q-input outlined label="Slutt" :model-value="endTime">
        <template v-slot:append>
          <q-icon name="access_time" class="cursor-pointer">
            <q-popup-proxy
              cover
              transition-show="scale"
              transition-hide="scale"
            >
              <q-time v-model="endDateTime" format24h mask="YYYY-MM-DDTHH:mm">
                <div class="row items-center justify-end">
                  <q-btn v-close-popup label="Close" color="primary" flat />
                </div>
              </q-time>
            </q-popup-proxy>
          </q-icon> </template
      ></q-input>
      <q-card bordered flat>
        <q-list separator>
          <q-item v-for="resource in resources" :key="resource.eventResourceId">
            <q-item-section>
              <q-item-label
                >{{ resource.resourceType.name }}
                <q-badge> {{ resource.minimumStaff }}</q-badge></q-item-label
              ></q-item-section
            >
            <q-item-section side>
              <q-item-label>{{
                formatStartEndTime(resource)
              }}</q-item-label></q-item-section
            >
            <q-item-section side
              ><q-btn flat round icon="delete"></q-btn
            ></q-item-section> </q-item
        ></q-list>
        <q-card-actions align="right">
          <q-btn
            no-caps
            dense
            flat
            label="Legg til vakt"
            color="primary"
            icon="add"
            @click="addResource"
          ></q-btn
        ></q-card-actions>
      </q-card>
      <q-dialog v-model="showingEdit">
        <q-card>
          <q-card-section class="q-gutter-md">
            <q-select
              label="Vakt"
              outlined
              :options="resourceTypes"
              option-label="name"
              option-value="resourceTypeId"
              v-model="selectedResource.resourceType"
            ></q-select>
            <q-input
              outlined
              label="Antall"
              suffix="stk"
              type="number"
              v-model="selectedResource.minimumStaff"
            ></q-input>
            <q-input outlined label="Start" :model-value="resourceStartTime">
              <template v-slot:append>
                <q-icon name="access_time" class="cursor-pointer">
                  <q-popup-proxy
                    cover
                    transition-show="scale"
                    transition-hide="scale"
                  >
                    <q-time
                      v-model="selectedResource.startDateTime"
                      format24h
                      mask="YYYY-MM-DDTHH:mm"
                    >
                      <div class="row items-center justify-end">
                        <q-btn
                          v-close-popup
                          label="Close"
                          color="primary"
                          flat
                        />
                      </div>
                    </q-time>
                  </q-popup-proxy>
                </q-icon> </template
            ></q-input>
            <q-input outlined label="Slutt" :model-value="resourceEndTime">
              <template v-slot:append>
                <q-icon name="access_time" class="cursor-pointer">
                  <q-popup-proxy
                    cover
                    transition-show="scale"
                    transition-hide="scale"
                  >
                    <q-time
                      v-model="selectedResource.endDateTime"
                      format24h
                      mask="YYYY-MM-DDTHH:mm"
                    >
                      <div class="row items-center justify-end">
                        <q-btn
                          v-close-popup
                          label="Close"
                          color="primary"
                          flat
                        />
                      </div>
                    </q-time>
                  </q-popup-proxy>
                </q-icon> </template></q-input
          ></q-card-section>
          <q-separator></q-separator>
          <q-card-actions align="right">
            <q-btn
              no-caps
              flat
              label="Avbryt"
              @click="showingEdit = false"
            ></q-btn>
            <q-btn
              no-caps
              unelevated
              color="primary"
              label="Lagre"
              @click="saveResource"
            ></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>
    </div>
  </q-page>
</template>

<script setup>
import { computed, inject, onMounted, ref, reactive } from "vue";
import { useQuasar } from "quasar";
import { useEventStore } from "stores/EventStore";
import {
  parseISO,
  format,
  addHours,
  addMinutes,
  formatISO,
  addDays,
  isBefore,
  isAfter,
} from "date-fns";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import { uid } from "quasar";
import { watch } from "vue";

const emit = defineEmits(["toggle-right"]);
const loading = false;
const $q = useQuasar();
const $router = useRouter();
const eventStore = useEventStore();

onMounted(() => {
  eventStore.getResourceTypes();
});

const resourceTypes = computed(() => eventStore.resourceTypes);

const name = ref(null);
const startDateTime = ref(formatISO(new Date()));
const date = computed(() => {
  return startDateTime.value
    ? format(parseISO(startDateTime.value), "dd.MM.yyyy")
    : null;
});
const startTime = computed(() => {
  return startDateTime.value
    ? format(parseISO(startDateTime.value), "HH:mm")
    : null;
});

const endDateTime = ref(formatISO(new Date()));

const endTime = computed(() => {
  return endDateTime.value
    ? format(parseISO(endDateTime.value), "HH:mm")
    : null;
});

watch(startDateTime, (newValue) => {
  endDateTime.value =
    formatISO(parseISO(newValue), { representation: "date" }) +
    "T" +
    formatISO(parseISO(endDateTime.value ?? newValue), {
      representation: "time",
    });
});

const resources = ref([]);

const selectedResource = ref(null);
const resourceStartTime = computed(() => {
  return selectedResource.value?.startDateTime
    ? format(parseISO(selectedResource.value.startDateTime), "HH:mm")
    : null;
});
const resourceEndTime = computed(() => {
  return selectedResource.value?.endDateTime
    ? format(parseISO(selectedResource.value.endDateTime), "HH:mm")
    : null;
});

const showingEdit = ref(false);

function addResource() {
  selectedResource.value = {
    eventResourceId: uid(),
    resourceType: null,
    startDateTime: startDateTime.value
      ? formatISO(addMinutes(parseISO(startDateTime.value), -30))
      : null,
    endDateTime: endDateTime.value
      ? formatISO(addMinutes(parseISO(endDateTime.value), 30))
      : null,
    minimumStaff: 1,
  };
  showingEdit.value = true;
}

function saveResource() {
  resources.value.push(selectedResource.value);
  showingEdit.value = false;
}

function formatTime(isoDateTime) {
  const date = parseISO(isoDateTime);
  return format(date, "HH:mm");
}

function formatStartEndTime(event) {
  return `${formatTime(event.startDateTime)}-${formatTime(event.endDateTime)}`;
}
</script>
