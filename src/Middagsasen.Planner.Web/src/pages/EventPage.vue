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
          @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
        ></q-input>
        <q-input
          :error="!isValidStartDate"
          autofocus
          outlined
          label="Dato"
          mask="##.##.####"
          placeholder="DD.MM.ÅÅÅÅ"
          @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
          v-model="startDate"
          ><template v-slot:append>
            <q-icon name="event" class="cursor-pointer">
              <q-popup-proxy transition-show="scale" transition-hide="scale">
                <q-date v-model="startDate" :mask="QUASAR_DATE_MASK">
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
          @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
          v-model="startTime"
        >
          <template v-slot:append>
            <q-icon name="access_time" class="cursor-pointer">
              <q-popup-proxy transition-show="scale" transition-hide="scale">
                <q-time v-model="startTime" format24h :mask="QUASAR_TIME_MASK">
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
          @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
        >
          <template v-slot:append>
            <q-icon name="access_time" class="cursor-pointer">
              <q-popup-proxy transition-show="scale" transition-hide="scale">
                <q-time v-model="endTime" format24h :mask="QUASAR_TIME_MASK">
                  <div class="row items-center justify-end">
                    <q-btn v-close-popup label="Lukk" color="primary" flat />
                  </div>
                </q-time>
              </q-popup-proxy>
            </q-icon> </template
        ></q-input>
        <q-card bordered flat>
          <q-list separator>
            <q-item
              v-for="resource in visibleResources"
              :key="resource.clientKey"
            >
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
    <div class="q-mt-lg text-center">
      <q-btn
        v-if="props.id"
        @click="showCreateTemplate"
        icon="file_copy"
        no-caps
        unelevated
        color="primary"
        label="Opprett mal"
      ></q-btn>
    </div>
    <div class="q-mt-xl text-center">
      <q-btn
        v-if="props.id"
        @click="confirmDeleteEvent"
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
            @click="createTemplate(props.id!)"
          ></q-btn>
        </q-card-actions>
        <q-inner-loading :showing="savingTemplate">
          <q-spinner size="3em" color="primary"></q-spinner>
        </q-inner-loading>
      </q-card>
    </q-dialog>
    <q-dialog v-model="showingEdit" persistent>
      <q-card class="full-width">
        <q-card-section class="row">
          <div>Vakt</div>
          <q-space></q-space>
          <q-btn
            v-if="selectedResource!.id"
            color="negative"
            flat
            round
            icon="delete"
            @click="deleteResource(selectedResource!)"
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
            v-model="selectedResource!.resourceType"
            @update:model-value="resourceTypeChanged"
          ></q-select>
          <q-input
            outlined
            @focus="
              (event) => (event.target as HTMLInputElement | null)?.select?.()
            "
            label="Minste bemanning"
            suffix="stk"
            step="1"
            type="number"
            v-model="selectedResource!.minimumStaff"
          ></q-input>

          <q-input
            outlined
            label="Start"
            mask="##:##"
            placeholder="TT:MM"
            @focus="
              (event) => (event.target as HTMLInputElement | null)?.select?.()
            "
            v-model="selectedResource!.startTime"
          >
            <template v-slot:append>
              <q-icon name="access_time" class="cursor-pointer">
                <q-popup-proxy transition-show="scale" transition-hide="scale">
                  <q-time
                    v-model="selectedResource!.startTime"
                    format24h
                    :mask="QUASAR_TIME_MASK"
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
            v-model="selectedResource!.endTime"
            @focus="
              (event) => (event.target as HTMLInputElement | null)?.select?.()
            "
          >
            <template v-slot:append>
              <q-icon name="access_time" class="cursor-pointer">
                <q-popup-proxy transition-show="scale" transition-hide="scale">
                  <q-time
                    v-model="selectedResource!.endTime"
                    format24h
                    :mask="QUASAR_TIME_MASK"
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

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useEventStore } from "stores/EventStore";
import { useRouter } from "vue-router";
import type { EventRequest, ResourceTypeResponse } from "src/types";
import {
  QUASAR_DATE_MASK,
  QUASAR_TIME_MASK,
  formatDate,
  formatTime,
  intervalOn,
  isDayKey,
  isValidDate,
  isValidTime,
  offsetTime,
  toDayKey,
  toLocalWire,
  today,
} from "src/shared/time";
import { toResourceDateTimes } from "src/shared/timeValidation";
import { getApiErrorMessage } from "src/shared/apiError";
import { newClientKey } from "src/shared/clientKey";

// Vakt i skjemaet: lastet fra eventet (med id/eventId), lagt til lokalt, eller
// en ny vakt under redigering (isNew, uten isDeleted).
interface ResourceForm {
  id?: number;
  // Stabil nøkkel for `:key` i lista. Sendes ikke til API-et.
  clientKey: string;
  eventId?: number;
  resourceType: ResourceTypeResponse | null;
  startTime: string | null;
  endTime: string | null;
  minimumStaff: number;
  isDeleted?: boolean;
  isNew?: boolean;
}

// Vakt i lista: har alltid vakttype og isDeleted.
interface ResourceModel extends ResourceForm {
  resourceType: ResourceTypeResponse;
  isDeleted: boolean;
}

defineEmits<{ "toggle-right": [] }>();
const loading = ref(false);
const $q = useQuasar();
const $router = useRouter();
const eventStore = useEventStore();

const props = withDefaults(
  defineProps<{
    date?: string;
    id?: string | null;
  }>(),
  {
    date: () => today(),
    id: null,
  }
);

onMounted(async () => {
  try {
    loading.value = true;
    eventStore.getResourceTypes();

    if (props.id) {
      await eventStore.getEvent(props.id);
      // getEvent setter selectedEvent (kaster ellers).
      const event = eventStore.selectedEvent!;
      name.value = event.name;
      startDate.value = formatDate(event.startTime);
      startTime.value = formatTime(event.startTime);
      endTime.value = formatTime(event.endTime);
      resources.value = event.resources.map((r) => {
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
    }
  } catch {
  } finally {
    loading.value = false;
  }
});

const resourceTypes = computed(() => eventStore.resourceTypes);

const name = ref<string | null>(null);

const isValidStartDate = computed(() => isValidDate(startDate.value));
const isValidStartTime = computed(() => isValidTime(startTime.value));
const isValidEndTime = computed(() => isValidTime(endTime.value));

// Slutt før start betyr at vaktlista går over midnatt (neste dag).
const interval = computed(() =>
  intervalOn(startDate.value, startTime.value, endTime.value)
);

const startDate = ref(formatDate(new Date()));
const startTime = ref("10:00");
const endTime = ref("17:00");
const resources = ref<ResourceModel[]>([]);

const canSave = computed(() => {
  return !!(
    name.value &&
    isValidStartDate.value &&
    isValidStartTime.value &&
    isValidEndTime.value &&
    resources.value.length
  );
});

const visibleResources = computed(() =>
  resources.value.filter((r) => !r.isDeleted)
);

// null til vaktdialogen åpnes; malen bruker `selectedResource!` fordi
// dialoginnholdet kun rendres når den er satt.
const selectedResource = ref<ResourceForm | null>(null);
const showingEdit = ref(false);

function resourceTypeChanged(newValue: ResourceTypeResponse | null) {
  if (newValue && newValue.defaultStaff) {
    selectedResource.value!.minimumStaff = newValue.defaultStaff;
  }
}

function addResource() {
  selectedResource.value = {
    resourceType: null,
    clientKey: newClientKey(),
    // Ugyldig dato eller tid på vaktlista gir tomt felt.
    startTime: isValidStartDate.value ? offsetTime(startTime.value, -30) : null,
    endTime: isValidStartDate.value ? offsetTime(endTime.value, 30) : null,
    minimumStaff: 1,
    isNew: true,
  };
  showingEdit.value = true;
}

function editResource(resource: ResourceModel) {
  selectedResource.value = resource;
  showingEdit.value = true;
}

function saveResource() {
  if (selectedResource.value?.isNew) {
    resources.value.push({
      clientKey: selectedResource.value.clientKey,
      // Lagre-knappen er deaktivert uten vakttype (canAdd).
      resourceType: selectedResource.value.resourceType!,
      startTime: selectedResource.value.startTime,
      endTime: selectedResource.value.endTime,
      minimumStaff: selectedResource.value.minimumStaff,
      isDeleted: false,
    });
  }
  showingEdit.value = false;
}

function deleteResource(resource: ResourceForm) {
  resource.isDeleted = true;
  showingEdit.value = false;
}

const canAdd = computed(() => {
  // Evalueres kun fra vaktdialogen, når selectedResource er satt.
  return !!(
    selectedResource.value!.resourceType &&
    selectedResource.value!.startTime &&
    selectedResource.value!.endTime &&
    selectedResource.value!.minimumStaff > 0
  );
});

async function saveEvent() {
  // Ugyldige vakttider (f.eks. «1») ville gitt RangeError i toLocalWire.
  const resourceTimes = resources.value.map((r) =>
    toResourceDateTimes(startDate.value, r.startTime, r.endTime)
  );
  const invalidIndex = resourceTimes.findIndex((t) => t === null);
  if (invalidIndex >= 0) {
    const invalid = resources.value[invalidIndex];
    $q.notify({
      message: `Vakta «${
        invalid?.resourceType.name ?? ""
      }» har ugyldig start- eller sluttid.`,
    });
    return;
  }
  try {
    loading.value = true;
    const model: EventRequest = {
      // canSave krever navn.
      name: name.value!,
      startTime: toLocalWire(interval.value.start),
      endTime: toLocalWire(interval.value.end),
      resources: resources.value.map((r, i) => {
        // Validert over: ingen er null.
        const times = resourceTimes[i]!;
        return {
          // `?? null`: id er valgfri i skjemaet, og med
          // exactOptionalPropertyTypes kan den ikke være undefined.
          id: r.id ?? null,
          resourceTypeId: r.resourceType.id,
          startTime: toLocalWire(times.start),
          endTime: toLocalWire(times.end),
          // q-input type="number" kan gi string; Number() sender et tall.
          minimumStaff: Number(r.minimumStaff),
          isDeleted: r.isDeleted,
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
    const date = toDayKey(interval.value.start);
    await $router.push(`/day/${date}`);
  } catch (error) {
    console.log(error);
    $q.notify({
      message: getApiErrorMessage(error, "Klarte ikke å lagre vaktlista."),
    });
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
    // Slett-knappen vises kun for et lastet event (props.id).
    const event = eventStore.selectedEvent!;
    const date = toDayKey(event.startTime);
    await eventStore.deleteEvent(event.id);
    $q.notify({ message: "Vaktlista er slettet." });
    $router.push(`/day/${date}`);
  } catch (error) {
    console.log(error);
    $q.notify({
      message: getApiErrorMessage(error, "Klarte ikke å slette vaktlista."),
    });
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
// id: dialogen åpnes kun via «Opprett mal», som vises når props.id er satt
// (derav `props.id!` i malen).
async function createTemplate(id: string) {
  try {
    savingTemplate.value = true;
    // Lagre-knappen er deaktivert uten navn.
    await eventStore.createTemplateFromEvent(id, templateName.value!);
    $q.notify({ message: "Ny mal opprettet." });
    showingCreateTemplate.value = false;
  } catch {
    $q.notify({ message: "Noe feilet mens malen skulle lagres." });
  } finally {
    savingTemplate.value = false;
  }
}
</script>
