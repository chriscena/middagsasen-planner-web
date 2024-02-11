<template>
  <q-card
    ><q-form @submit="saveEvent"
      ><q-card-section class="row">
        <q-btn
          flat
          dense
          round
          icon="close"
          @click="emit('cancel')"
          title="Lukk"
        ></q-btn>
        <div class="text-h6">Vaktliste</div>
        <q-space></q-space>
        <q-btn
          color="primary"
          flat
          label="Lagre"
          type="submit"
          :disable="!canSave"
          no-caps
        ></q-btn>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-section class="q-gutter-sm">
        <q-input
          outlined
          label="Navn"
          v-model="name"
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
        ></q-input>
        <q-input
          outlined
          label="Beskrivelse"
          v-model="description"
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
          type="textarea"
          autogrow
        ></q-input>
        <DatePickerInput
          label="Dato"
          autofocus
          v-model="startDate"
          :error="!isValidDate"
        ></DatePickerInput>
        <TimePickerInput
          label="Start"
          v-model="startTime"
          :error="!isValidStartTime"
        ></TimePickerInput>
        <TimePickerInput
          label="Slutt"
          v-model="endTime"
          :error="!isValidEndTime"
        ></TimePickerInput>
        <ResourceList
          v-model="resources"
          :resource-types="resourceTypes"
          :startTime="startTime"
          :endTime="endTime"
        ></ResourceList>
      </q-card-section>
    </q-form>
    <q-card-section class="q-mt-lg text-center">
      <q-btn
        v-if="props.id"
        @click="showCreateTemplate"
        icon="file_copy"
        no-caps
        unelevated
        color="primary"
        label="Opprett mal"
      ></q-btn>
    </q-card-section>
    <q-card-section class="q-mt-xl text-center">
      <q-btn
        v-if="props.id"
        @click="confirmDeleteEvent"
        icon="delete"
        no-caps
        unelevated
        color="negative"
        label="Slett vaktliste"
      ></q-btn>
    </q-card-section>
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
    <q-dialog v-model="showingCreateTemplate">
      <q-card>
        <q-card-section class="text-h6"> Opprette mal </q-card-section>
        <q-card-section>
          <q-input outlined label="Navn på mal" v-model="templateName"></q-input
        ></q-card-section>
        <q-card-actions align="right">
          <q-btn
            no-caps
            flat
            label="Avbryt"
            color="primary"
            @click="showingCreateTemplate = false"
          ></q-btn>
          <q-btn
            no-caps
            unelevated
            label="Lagre"
            color="primary"
            :disable="!templateName"
            @click="createTemplate(props.id)"
          ></q-btn>
        </q-card-actions>
        <q-inner-loading :showing="savingTemplate">
          <q-spinner size="3em" color="primary"></q-spinner>
        </q-inner-loading>
      </q-card>
    </q-dialog>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-card>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useEventStore } from "stores/EventStore";
import {
  parseISO,
  format,
  isValid,
  formatISO,
  parse,
  isBefore,
  addDays,
} from "date-fns";
import { useRouter } from "vue-router";
import TimePickerInput from "components/TimePickerInput.vue";
import DatePickerInput from "components/DatePickerInput.vue";
import ResourceList from "components/ResourceList.vue";

const emit = defineEmits(["cancel", "saved", "deleted"]);
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
    type: Number,
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
      description.value = event.description;
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
const description = ref(null);

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
const endTime = ref("17:00");
const resources = ref([]);

const canSave = computed(() => {
  return !!(name.value && startDate.value && startTime.value && endTime.value);
});

const selectedResource = ref(null);
const showingEdit = ref(false);

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
      description: description.value,
      startTime: formatDateTime(startDateTime.value),
      endTime: formatDateTime(endDateTime.value),
      resources: resources.value.map((r) => {
        return {
          id: r.id,
          resourceTypeId: r.resourceType.id,
          startTime: formatDateTime(toDateTime(startDate.value, r.startTime)),
          endTime: formatDateTime(toDateTime(startDate.value, r.endTime)),
          minimumStaff: r.minimumStaff,
          isDeleted: r.isDeleted,
          shifts: [],
        };
      }),
    };
    if (props.id) {
      await eventStore.updateEvent(props.id, model);
      $q.notify({
        message: "Endringer i vaktlista er lagret.",
      });
    } else {
      await eventStore.addEvent(model);
      $q.notify({
        message: "Vaktlista er lagt til.",
      });
    }
    emit("saved", model);
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
    const model = await eventStore.deleteEvent(event.id);
    $q.notify({ message: "Vaktlista er slettet." });
    emit("deleted", model);
  } catch (error) {
  } finally {
    loading.value = false;
  }
}

function formatDateTime(date) {
  return format(date, "yyyy'-'MM'-'dd'T'HH':'mm", new Date());
}

const showingCreateTemplate = ref(false);
const templateName = ref(null);
function showCreateTemplate() {
  templateName.value = null;
  showingCreateTemplate.value = true;
}

const savingTemplate = ref(false);
async function createTemplate(id) {
  try {
    savingTemplate.value = true;
    await eventStore.createTemplateFromEvent(id, templateName.value);
    $q.notify({ message: "Ny mal opprettet." });
    showingCreateTemplate.value = false;
  } catch (error) {
    $q.notify({ message: "Noe feilet mens malen skulle lagres." });
  } finally {
    savingTemplate.value = false;
  }
}
</script>
