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
          :disable="!canSave || loadFailed"
          no-caps
        ></q-btn>
      </q-card-section>
      <q-separator></q-separator>
      <q-card-section class="q-gutter-sm">
        <q-input
          outlined
          label="Navn"
          v-model="name"
          @focus="
            (event) => (event.target as HTMLInputElement | null)?.select?.()
          "
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
          :error="!isValidStartDate"
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
        :disable="loadFailed"
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
        :disable="loadFailed"
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
import { useEventStore } from "@/stores/EventStore";
import TimePickerInput from "@/components/TimePickerInput.vue";
import DatePickerInput from "@/components/DatePickerInput.vue";
import ResourceList from "@/components/ResourceList.vue";
import type { ResourceFormModel } from "@/components/ResourceForm.vue";
import type { EventRequest } from "@/types";
import {
  formatDate,
  formatTime,
  intervalOn,
  isDayKey,
  isValidDate,
  isValidTime,
  toLocalWire,
  today,
} from "@/shared/time";
import { notifyApiError } from "@/shared/notifyApiError";
import {
  findInvalidResource,
  toResourceRequests,
} from "@/shared/resourceRequests";
import { newClientKey } from "@/shared/clientKey";

const emit = defineEmits<{
  cancel: [];
  saved: [value: EventRequest];
  deleted: [];
}>();
const loading = ref(false);
// Settes når lasting av vaktlista feiler. Skjemaet står da med standardverdier,
// så Lagre/Slett/Opprett mal sperres for ikke å overskrive eller slette feil data.
const loadFailed = ref(false);
const $q = useQuasar();
const eventStore = useEventStore();

const props = withDefaults(
  defineProps<{
    date?: string;
    id?: number | null;
  }>(),
  {
    date: () => today(),
    id: null,
  }
);

onMounted(async () => {
  try {
    loading.value = true;

    if (props.id) {
      await Promise.all([
        eventStore.getResourceTypes(),
        eventStore.getEvent(props.id),
      ]);
      const event = eventStore.selectedEvent;
      // Tidligere ga null her en TypeError som ble svelget av catch under.
      if (!event) {
        loadFailed.value = true;
        $q.notify({
          type: "negative",
          message: "Klarte ikke å hente vaktlista.",
        });
        return;
      }
      name.value = event.name;
      description.value = event.description;
      startDate.value = formatDate(event.startTime);
      startTime.value = formatTime(event.startTime);
      endTime.value = formatTime(event.endTime);
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
      // Ugyldig dato i URL-en (/create/:date) gir dagens dato.
      startDate.value = formatDate(isDayKey(props.date) ? props.date : today());
      name.value = "Åpningstid";
      await eventStore.getResourceTypes();
    }
  } catch (error) {
    loadFailed.value = true;
    notifyApiError(error, "Klarte ikke å hente vaktlista.");
  } finally {
    loading.value = false;
  }
});

const resourceTypes = computed(() => eventStore.resourceTypes);

const name = ref<string | null>(null);
const description = ref<string | null | undefined>(null);

const isValidStartDate = computed(() => isValidDate(startDate.value));
const isValidStartTime = computed(() => isValidTime(startTime.value));
const isValidEndTime = computed(() => isValidTime(endTime.value));

// Slutt før start betyr at vaktlista går over midnatt (neste dag).
const interval = computed(() =>
  intervalOn(startDate.value, startTime.value, endTime.value)
);

const startDate = ref<string | null>(formatDate(new Date()));
const startTime = ref<string | null>("10:00");
const endTime = ref<string | null>("17:00");
const resources = ref<ResourceFormModel[]>([]);

const canSave = computed(() => {
  return !!(
    name.value &&
    isValidStartDate.value &&
    isValidStartTime.value &&
    isValidEndTime.value
  );
});

async function saveEvent() {
  if (loadFailed.value) return;
  // Lagre-knappen er deaktivert uten canSave, men skjemaet kan sendes med
  // Enter; ugyldig dato eller tid ville gitt RangeError i toLocalWire.
  if (!canSave.value) return;
  // Ugyldige vakttider (f.eks. «1») ville gitt RangeError i toTimeWire.
  // Slettede vakter sjekkes ikke (se findInvalidResource).
  const invalid = findInvalidResource(resources.value);
  if (invalid) {
    $q.notify({
      message: `Vakta «${
        invalid.resourceType?.name ?? ""
      }» har ugyldig start- eller sluttid.`,
    });
    return;
  }
  try {
    loading.value = true;
    const model: EventRequest = {
      // Lagre-knappen er deaktivert uten navn (canSave).
      name: name.value!,
      description: description.value ?? null,
      startTime: toLocalWire(interval.value.start),
      endTime: toLocalWire(interval.value.end),
      resources: toResourceRequests(resources.value),
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
    notifyApiError(error, "Klarte ikke å lagre vaktlista.");
  } finally {
    loading.value = false;
  }
}

const showingDelete = ref<boolean | null>(null);
function confirmDeleteEvent() {
  showingDelete.value = true;
}

async function deleteEvent() {
  // props.id er vaktlista som faktisk er åpen; selectedEvent kan peke på en annen.
  if (loadFailed.value || !props.id) return;
  try {
    loading.value = true;
    showingDelete.value = false;
    await eventStore.deleteEvent(props.id);
    $q.notify({ message: "Vaktlista er slettet." });
    emit("deleted");
  } catch (error) {
    notifyApiError(error, "Klarte ikke å slette vaktlista.");
  } finally {
    loading.value = false;
  }
}

const showingCreateTemplate = ref(false);
const templateName = ref<string | null>(null);
function showCreateTemplate() {
  templateName.value = null;
  showingCreateTemplate.value = true;
}

const savingTemplate = ref(false);
async function createTemplate(id: number | null) {
  if (loadFailed.value) return;
  try {
    savingTemplate.value = true;
    // Knappen vises kun med id, og Lagre er deaktivert uten malnavn.
    await eventStore.createTemplateFromEvent(id!, templateName.value!);
    $q.notify({ message: "Ny mal opprettet." });
    showingCreateTemplate.value = false;
  } catch (error) {
    notifyApiError(error, "Noe feilet mens malen skulle lagres.");
  } finally {
    savingTemplate.value = false;
  }
}
</script>
