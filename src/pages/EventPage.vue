<template>
  <q-page padding>
    <q-form @submit="saveEvent">
      <q-header bordered
        ><q-toolbar class="bg-grey-1 text-blue-grey-8">
          <q-btn flat dense round icon="close" @click="$router.go(-1)"></q-btn>
          <q-toolbar-title>Vaktliste</q-toolbar-title>
          <q-space></q-space>
          <q-btn
            color="primary"
            flat
            label="Lagre"
            type="submit"
            :disable="!canSave"
            no-caps
          ></q-btn> </q-toolbar
      ></q-header>
      <div class="q-gutter-sm">
        <q-input
          outlined
          label="Navn"
          v-model="name"
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
        ></q-input>
        <q-input
          :error="!isValidDate"
          autofocus
          outlined
          label="Dato"
          mask="##.##.####"
          placeholder="DD.MM.ÅÅÅÅ"
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
          v-model="startDate"
          ><template v-slot:append>
            <q-icon name="event" class="cursor-pointer">
              <q-popup-proxy transition-show="scale" transition-hide="scale">
                <q-date v-model="startDate" mask="DD.MM.YYYY">
                  <div class="row items-center justify-end">
                    <q-btn v-close-popup label="Lukk" color="primary" flat />
                  </div>
                </q-date>
              </q-popup-proxy>
            </q-icon> </template
        ></q-input>
        <q-input
          outlined
          :error="!isValidStartTime"
          label="Start"
          mask="##:##"
          placeholder="TT:MM"
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
          v-model="startTime"
        >
          <template v-slot:append>
            <q-icon name="access_time" class="cursor-pointer">
              <q-popup-proxy
                v-model="showingStartTimePicker"
                transition-show="scale"
                transition-hide="scale"
              >
                <q-time v-model="startTime" format24h mask="HH:mm">
                  <div class="row items-center justify-end">
                    <q-btn v-close-popup label="Lukk" color="primary" flat />
                  </div>
                </q-time>
              </q-popup-proxy>
            </q-icon> </template
        ></q-input>
        <q-input
          outlined
          label="Slutt"
          mask="##:##"
          placeholder="TT:MM"
          v-model="endTime"
          :error="!isValidEndTime"
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
        >
          <template v-slot:append>
            <q-icon name="access_time" class="cursor-pointer">
              <q-popup-proxy
                v-model="showingEndTimePicker"
                transition-show="scale"
                transition-hide="scale"
              >
                <q-time v-model="endTime" format24h mask="HH:mm">
                  <div class="row items-center justify-end">
                    <q-btn v-close-popup label="Lukk" color="primary" flat />
                  </div>
                </q-time>
              </q-popup-proxy>
            </q-icon> </template
        ></q-input>
        <q-card bordered flat>
          <q-list separator>
            <q-item v-for="(resource, index) in visibleResources" :key="index">
              <q-item-section>
                <q-item-label
                  >{{ resource.resourceType.name }}
                  <q-badge> {{ resource.minimumStaff }}</q-badge></q-item-label
                ></q-item-section
              >
              <q-item-section side>
                <q-item-label
                  >{{ resource.startTime }}-{{ resource.endTime }}</q-item-label
                ></q-item-section
              >
              <q-item-section side
                ><q-btn
                  flat
                  round
                  icon="edit"
                  @click="editResource(resource)"
                ></q-btn
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
        </q-card></div
    ></q-form>
    <div class="text-center">
      <q-btn
        v-if="props.id"
        @click="confirmDeleteEvent"
        class="q-mt-xl"
        icon="delete"
        no-caps
        unelevated
        color="negative"
        label="Slett vaktliste"
      ></q-btn>
    </div>
    <q-dialog v-model="showingDelete">
      <q-card>
        <q-card-section> Vil du slette denne vaktlista? </q-card-section>
        <q-card-actions align="right">
          <q-btn
            no-caps
            flat
            label="Avbryt"
            color="primary"
            @click="showingDelete = false"
          ></q-btn>
          <q-btn
            no-caps
            flat
            label="Slett"
            color="primary"
            @click="deleteEvent(props.id)"
          ></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>
    <q-dialog v-model="showingEdit" persistent>
      <q-card class="full-width">
        <q-card-section class="row">
          <div>Vakt</div>
          <q-space></q-space>
          <q-btn
            v-if="selectedResource.id"
            color="negative"
            flat
            round
            icon="delete"
            @click="deleteResource(selectedResource)"
          ></q-btn>
        </q-card-section>
        <q-card-section class="q-gutter-md">
          <q-select
            autofocus
            label="Vakt"
            outlined
            :options="resourceTypes"
            option-label="name"
            option-value="resourceTypeId"
            v-model="selectedResource.resourceType"
            @update:model-value="resourceTypeChanged"
          ></q-select>
          <q-input
            outlined
            @focus="
              (event) => (event.target?.select ? event.target.select() : _)
            "
            label="Minste bemanning"
            suffix="stk"
            step="1"
            type="number"
            v-model="selectedResource.minimumStaff"
          ></q-input>

          <q-input
            outlined
            label="Start"
            mask="##:##"
            placeholder="TT:MM"
            @focus="
              (event) => (event.target?.select ? event.target.select() : _)
            "
            v-model="selectedResource.startTime"
          >
            <template v-slot:append>
              <q-icon name="access_time" class="cursor-pointer">
                <q-popup-proxy transition-show="scale" transition-hide="scale">
                  <q-time
                    v-model="selectedResource.startTime"
                    format24h
                    mask="HH:mm"
                  >
                    <div class="row items-center justify-end">
                      <q-btn v-close-popup label="Lukk" color="primary" flat />
                    </div>
                  </q-time>
                </q-popup-proxy>
              </q-icon> </template
          ></q-input>
          <q-input
            outlined
            label="Slutt"
            mask="##:##"
            placeholder="TT:MM"
            v-model="selectedResource.endTime"
            @focus="
              (event) => (event.target?.select ? event.target.select() : _)
            "
          >
            <template v-slot:append>
              <q-icon name="access_time" class="cursor-pointer">
                <q-popup-proxy transition-show="scale" transition-hide="scale">
                  <q-time
                    v-model="selectedResource.endTime"
                    format24h
                    mask="HH:mm"
                  >
                    <div class="row items-center justify-end">
                      <q-btn v-close-popup label="Lukk" color="primary" flat />
                    </div>
                  </q-time>
                </q-popup-proxy>
              </q-icon> </template></q-input
        ></q-card-section>
        <q-card-actions align="right">
          <q-btn
            no-caps
            flat
            color="primary"
            label="Avbryt"
            @click="showingEdit = false"
          ></q-btn>
          <q-btn
            no-caps
            unelevated
            color="primary"
            label="Lagre"
            :disable="!canAdd"
            @click="saveResource"
          ></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-page>
</template>

<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useQuasar } from "quasar";
import { useEventStore } from "stores/EventStore";
import {
  parseISO,
  format,
  isValid,
  addMinutes,
  formatISO,
  parse,
  isBefore,
  addDays,
} from "date-fns";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";

const emit = defineEmits(["toggle-right"]);
const loading = ref(false);
const $q = useQuasar();
const $router = useRouter();
const eventStore = useEventStore();

const props = defineProps({
  date: {
    type: String,
    default: () => formatISO(new Date(), { representation: "date" }),
  },
  id: {
    type: String,
    default: null,
  },
});

onMounted(async () => {
  try {
    loading.value = true;
    eventStore.getResourceTypes();

    if (props.id) {
      await eventStore.getEvent(props.id);
      const event = eventStore.selectedEvent;
      name.value = event.name;
      startDate.value = formatDate(new Date(event.startTime));
      startTime.value = formatTime(new Date(event.startTime));
      endTime.value = formatTime(new Date(event.endTime));
      resources.value = event.resources.map((r) => {
        return {
          id: r.id,
          eventId: r.eventId,
          resourceType: r.resourceType,
          startTime: formatTime(r.startTime),
          endTime: formatTime(r.endTime),
          minimumStaff: r.minimumStaff,
          isDeleted: false,
        };
      });
    } else {
      startDate.value = format(
        parse(props.date, "yyyy-MM-dd", new Date()),
        "dd.MM.yyyy"
      );
      name.value = "Åpningstid";
    }
  } catch (error) {
  } finally {
    loading.value = false;
  }
});

const resourceTypes = computed(() => eventStore.resourceTypes);

const name = ref(null);

const isValidDate = computed(() =>
  isValid(parse(startDate.value, "dd.MM.yyyy", new Date()))
);
const isValidStartTime = computed(() =>
  isValid(parse(startTime.value, "HH:mm", new Date()))
);
const isValidEndTime = computed(() =>
  isValid(parse(endTime.value, "HH:mm", new Date()))
);

const startDateTime = computed(() => {
  try {
    return toDateTime(startDate.value, startTime.value);
  } catch (error) {
    console.log(error);
    return null;
  }
});

const endDateTime = computed(() => {
  try {
    return toDateTime(startDate.value, endTime.value, startDateTime.value);
  } catch (error) {
    console.log(error);
    return null;
  }
});

function toDateTime(date, time, start) {
  const datetime = parse(`${date} ${time}`, "dd.MM.yyyy HH:mm", new Date());
  if (start && isBefore(datetime, start)) datetime = addDays(datetime, 1);
  return datetime;
}

const startDate = ref(formatDate(new Date()));
const startTime = ref("10:00");

const showingStartTimePicker = ref(false);

const endTime = ref("17:00");

const showingEndTimePicker = ref(false);

const resources = ref([]);

const visibleResources = computed(() =>
  resources.value.filter((r) => !r.isDeleted)
);

const selectedResource = ref(null);

const showingEdit = ref(false);

const canSave = computed(() => {
  return !!(
    name.value &&
    // startDateTime.value &&
    // endDateTime.value &&
    resources.value.length
  );
});

function resourceTypeChanged(newValue) {
  if (newValue && newValue.defaultStaff) {
    selectedResource.value.minimumStaff = newValue.defaultStaff;
  }
}

function addResource() {
  selectedResource.value = {
    resourceType: null,
    startTime: startDateTime.value
      ? format(addMinutes(startDateTime.value, -30), "HH:mm")
      : null,
    endTime: endDateTime.value
      ? format(addMinutes(endDateTime.value, 30), "HH:mm")
      : null,
    minimumStaff: 1,
    isDeleted: false,
  };
  showingEdit.value = true;
}

function saveResource() {
  if (resources.value.includes(selectedResource.value));
  resources.value.push(selectedResource.value);
  showingEdit.value = false;
}

function deleteResource(resource) {
  resource.isDeleted = true;
  showingEdit.value = false;
}

function editResource(resource) {
  selectedResource.value = resource;
  showingEdit.value = true;
}

const canAdd = computed(() => {
  return !!(
    selectedResource.value.resourceType &&
    selectedResource.value.startTime &&
    selectedResource.value.endTime &&
    selectedResource.value.minimumStaff > 0
  );
});

function formatTime(isoDateTime) {
  if (isoDateTime instanceof Date) return format(isoDateTime, "HH:mm");
  return format(parseISO(isoDateTime), "HH:mm");
}

function formatDate(isoDateTime) {
  if (isoDateTime instanceof Date) return format(isoDateTime, "dd.MM.yyyy");
  return format(parseISO(isoDateTime), "dd.MM.yyyy");
}

async function saveEvent() {
  try {
    loading.value = true;
    const model = {
      name: name.value,
      startTime: formatISO(startDateTime.value),
      endTime: formatISO(endDateTime.value),
      resources: resources.value.map((r) => {
        return {
          id: r.id,
          resourceTypeId: r.resourceType.id,
          startTime: formatISO(toDateTime(startDate.value, r.startTime)),
          endTime: formatISO(toDateTime(startDate.value, r.endTime)),
          minimumStaff: r.minimumStaff,
          isDeleted: r.isDeleted,
          shifts: [],
        };
      }),
    };
    if (props.id) {
      await eventStore.updateEvent(props.id, model);
      $q.notify({
        message: "Endringer i vaktlista er lagret",
      });
    } else {
      await eventStore.addEvent(model);
      $q.notify({
        message: "Vaktlista er lagt til",
      });
    }
    const date = formatISO(startDateTime.value, {
      representation: "date",
    });
    await $router.push(`/day/${date}`);
  } catch (error) {
  } finally {
    loading.value = false;
  }
}

const showingDelete = ref(null);
function confirmDeleteEvent() {
  showingDelete.value = true;
}

async function deleteEvent() {
  try {
    loading.value = true;
    showingDelete.value = false;
    const event = eventStore.selectedEvent;
    const date = formatISO(parseISO(event.startTime), {
      representation: "date",
    });
    await eventStore.deleteEvent(event.id);
    $q.notify({ message: "Vaktlista er slettet." });
    $router.push(`/day/${date}`);
  } catch (error) {
  } finally {
    loading.value = false;
  }
}
</script>
