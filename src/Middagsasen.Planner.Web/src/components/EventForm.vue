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
          @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
        ></q-input>
        <q-input
          outlined
          label="Kommentar"
          v-model="description"
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
            @click="deleteEvent()"
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

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useEventStore } from "stores/EventStore";
import { parseISO, format, isValid, formatISO, parse } from "date-fns";
import TimePickerInput from "components/TimePickerInput.vue";
import DatePickerInput from "components/DatePickerInput.vue";
import ResourceList from "components/ResourceList.vue";
import type { ResourceFormModel } from "components/ResourceForm.vue";
import type { EventRequest } from "src/types";
import { toDateTime, toResourceStartDateTime } from "src/shared/eventDateTime";
import { newClientKey } from "src/shared/clientKey";

const emit = defineEmits<{
  cancel: [];
  saved: [value: EventRequest];
  deleted: [];
}>();
const loading = ref(false);
const $q = useQuasar();
const eventStore = useEventStore();

const props = withDefaults(
  defineProps<{
    date?: string;
    id?: number | null;
  }>(),
  {
    date: () => formatISO(new Date(), { representation: "date" }),
    id: null,
  }
);

onMounted(async () => {
  try {
    loading.value = true;
    eventStore.getResourceTypes();

    if (props.id) {
      await eventStore.getEvent(props.id);
      const event = eventStore.selectedEvent;
      // Tidligere ga null her en TypeError som ble svelget av catch under.
      if (!event) return;
      name.value = event.name;
      description.value = event.description;
      startDate.value = formatDate(new Date(event.startTime));
      startTime.value = formatTime(new Date(event.startTime));
      endTime.value = formatTime(new Date(event.endTime));
      resources.value = event.resources.map((r): ResourceFormModel => {
        return {
          id: r.id,
          clientKey: newClientKey(r.id),
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
  } catch {
  } finally {
    loading.value = false;
  }
});

const resourceTypes = computed(() => eventStore.resourceTypes);

const name = ref<string | null>(null);
const description = ref<string | null | undefined>(null);

// parse(null) og parse("") gir begge Invalid Date.
const isValidDate = computed(() =>
  isValid(parse(startDate.value ?? "", "dd.MM.yyyy", new Date()))
);
const isValidStartTime = computed(() =>
  isValid(parse(startTime.value ?? "", "HH:mm", new Date()))
);
const isValidEndTime = computed(() =>
  isValid(parse(endTime.value ?? "", "HH:mm", new Date()))
);

const startDateTime = computed(() =>
  toDateTime(startDate.value, startTime.value)
);

// Slutt før start betyr at vaktlista går over midnatt (neste dag).
const endDateTime = computed(() =>
  toDateTime(startDate.value, endTime.value, startDateTime.value)
);

const startDate = ref<string | null>(formatDate(new Date()));
const startTime = ref<string | null>("10:00");
const endTime = ref<string | null>("17:00");
const resources = ref<ResourceFormModel[]>([]);

const canSave = computed(() => {
  return !!(
    name.value &&
    isValidDate.value &&
    isValidStartTime.value &&
    isValidEndTime.value
  );
});

function formatTime(isoDateTime: string | Date) {
  if (isoDateTime instanceof Date) return format(isoDateTime, "HH:mm");
  return format(parseISO(isoDateTime), "HH:mm");
}

function formatDate(isoDateTime: string | Date) {
  if (isoDateTime instanceof Date) return format(isoDateTime, "dd.MM.yyyy");
  return format(parseISO(isoDateTime), "dd.MM.yyyy");
}

async function saveEvent() {
  try {
    loading.value = true;
    const model: EventRequest = {
      // Lagre-knappen er deaktivert uten navn (canSave).
      name: name.value!,
      description: description.value ?? null,
      startTime: formatDateTime(startDateTime.value),
      endTime: formatDateTime(endDateTime.value),
      resources: resources.value.map((r) => {
        const resourceStart = toResourceStartDateTime(
          startDate.value,
          r.startTime,
          startDateTime.value
        );
        return {
          id: r.id ?? null,
          // ResourceForm krever vakttype før lagring (canAdd).
          resourceTypeId: r.resourceType!.id,
          startTime: formatDateTime(resourceStart),
          endTime: formatDateTime(
            toDateTime(startDate.value, r.endTime, resourceStart)
          ),
          // q-input type="number" kan gi string; Number() sender et tall.
          minimumStaff: Number(r.minimumStaff),
          // Listeelementer har alltid isDeleted satt (false ved lasting og legg til).
          isDeleted: r.isDeleted ?? false,
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
  } catch {
  } finally {
    loading.value = false;
  }
}

const showingDelete = ref<boolean | null>(null);
function confirmDeleteEvent() {
  showingDelete.value = true;
}

async function deleteEvent() {
  try {
    loading.value = true;
    showingDelete.value = false;
    const event = eventStore.selectedEvent;
    // Tidligere ga null her en TypeError som ble svelget av catch under.
    if (!event) return;
    await eventStore.deleteEvent(event.id);
    $q.notify({ message: "Vaktlista er slettet." });
    emit("deleted");
  } catch {
  } finally {
    loading.value = false;
  }
}

function formatDateTime(date: Date) {
  return format(date, "yyyy'-'MM'-'dd'T'HH':'mm");
}

const showingCreateTemplate = ref(false);
const templateName = ref<string | null>(null);
function showCreateTemplate() {
  templateName.value = null;
  showingCreateTemplate.value = true;
}

const savingTemplate = ref(false);
async function createTemplate(id: number | null) {
  try {
    savingTemplate.value = true;
    // Knappen vises kun med id, og Lagre er deaktivert uten malnavn.
    await eventStore.createTemplateFromEvent(id!, templateName.value!);
    $q.notify({ message: "Ny mal opprettet." });
    showingCreateTemplate.value = false;
  } catch {
    $q.notify({ message: "Noe feilet mens malen skulle lagres." });
  } finally {
    savingTemplate.value = false;
  }
}
</script>
