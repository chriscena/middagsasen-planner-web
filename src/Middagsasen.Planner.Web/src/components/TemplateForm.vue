<template>
  <q-card>
    <q-form @submit="saveTemplate">
      <q-card-section class="row">
        <q-btn flat dense round icon="close" @click="emit('cancel')"></q-btn>
        <div class="text-h6">Mal</div>
        <q-space></q-space>
        <q-btn
          color="primary"
          flat
          label="Lagre"
          type="submit"
          :disable="!canSave"
          no-caps
        ></q-btn></q-card-section
      ><q-separator></q-separator>
      <q-card-section class="q-gutter-sm">
        <q-input
          autofocus
          outlined
          label="Navn på mal"
          v-model="name"
          @focus="
            (event) => (event.target as HTMLInputElement | null)?.select?.()
          "
        ></q-input>
        <q-input
          hide-bottom-space
          outlined
          label="Navn på vaktliste"
          v-model="eventName"
          @focus="
            (event) => (event.target as HTMLInputElement | null)?.select?.()
          "
        ></q-input>
        <TimePickerInput
          :error="!isValidStartTime"
          label="Start"
          v-model="startTime"
        ></TimePickerInput>
        <TimePickerInput
          :error="!isValidEndTime"
          label="Slutt"
          v-model="endTime"
        ></TimePickerInput>
        <ResourceList
          v-model="resources"
          :resource-types="resourceTypes"
          :startTime="startTime"
          :endTime="endTime"
        ></ResourceList>
        <CompetencyRequirementList
          v-model="competencyRequirements"
        ></CompetencyRequirementList>
      </q-card-section>

      <q-card-section class="q-mt-xl text-center">
        <q-btn
          v-if="modelValue.id"
          @click="confirmDeleteEvent"
          icon="delete"
          no-caps
          unelevated
          color="negative"
          label="Slett mal"
        ></q-btn>
      </q-card-section>
      ></q-form
    >
    <q-dialog v-model="showingDelete">
      <q-card>
        <q-card-section> Vil du slette denne malen? </q-card-section>
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
            @click="deleteTemplate"
          ></q-btn>
        </q-card-actions>
      </q-card>
    </q-dialog>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import TimePickerInput from "@/components/TimePickerInput.vue";
import ResourceList from "@/components/ResourceList.vue";
import CompetencyRequirementList from "@/components/CompetencyRequirementList.vue";
import type { ResourceFormModel } from "@/components/ResourceForm.vue";
import type {
  CompetencyRequirementRequest,
  EventTemplateResponse,
  ResourceTemplateRequest,
  ResourceTypeResponse,
} from "@/types";
import { newClientKey } from "@/shared/clientKey";
import { formatTime, isValidTime, toTimeWire } from "@/shared/time";
import {
  findInvalidResource,
  toResourceTemplateRequests,
  visibleResources,
} from "@/shared/resourceRequests";
import {
  areValidCompetencyRequirements,
  toCompetencyRequirementDrafts,
  toCompetencyRequirementRequests,
  type CompetencyRequirementDraft,
} from "@/shared/competencyRequirements";

// Malen slik TemplatesPage sender den: en EventTemplateResponse, eller en ny
// mal med id 0, name null og uten anleggskrav.
export type TemplateFormValue = Omit<
  EventTemplateResponse,
  "name" | "competencyRequirements"
> & {
  name: string | null;
  competencyRequirements?: EventTemplateResponse["competencyRequirements"];
};

// EventTemplateRequest + id (brukes av eventStore.updateTemplate/deleteTemplate).
// name/eventName kan være null fra skjemaet.
export interface TemplateFormModel {
  id: number;
  name: string | null;
  eventName: string | null;
  startTime: string;
  endTime: string;
  resourceTemplates: ResourceTemplateRequest[];
  // Hele lista sendes alltid; tom liste fjerner alle anleggskrav.
  competencyRequirements: CompetencyRequirementRequest[];
}

const emit = defineEmits<{
  cancel: [];
  save: [value: TemplateFormModel];
  delete: [value: Pick<TemplateFormModel, "id">];
}>();

const props = withDefaults(
  defineProps<{
    modelValue: TemplateFormValue;
    resourceTypes: ResourceTypeResponse[];
    loading?: boolean;
  }>(),
  {
    loading: false,
  }
);

onMounted(async () => {
  name.value = props.modelValue.name;
  eventName.value = props.modelValue.eventName;
  startTime.value = formatTime(props.modelValue.startTime);
  endTime.value = formatTime(props.modelValue.endTime);
  // resourceTemplates er nullable i DTO-en.
  resources.value = (props.modelValue.resourceTemplates ?? []).map(
    (r): ResourceFormModel => {
      return {
        id: r.id,
        clientKey: newClientKey(r.id),
        resourceType: r.resourceType,
        startTime: formatTime(r.startTime),
        endTime: formatTime(r.endTime),
        shiftCount: r.shiftCount,
        isDeleted: false,
      };
    }
  );
  competencyRequirements.value = toCompetencyRequirementDrafts(
    props.modelValue.competencyRequirements
  );
});

const name = ref<string | null>(null);
const eventName = ref<string | null>(null);

const isValidStartTime = computed(() => isValidTime(startTime.value));
const isValidEndTime = computed(() => isValidTime(endTime.value));
// Ugyldige tider (f.eks. «1») ville gitt RangeError i toTimeWire. Slettede
// oppgaver sjekkes ikke (se findInvalidResource).
const hasValidResourceTimes = computed(
  () => !findInvalidResource(resources.value)
);

const startTime = ref<string | null>("10:00");
const endTime = ref<string | null>("17:00");
const resources = ref<ResourceFormModel[]>([]);
const competencyRequirements = ref<CompetencyRequirementDraft[]>([]);

const canSave = computed(() => {
  // EventTemplateRequest krever både malnavn og navn på vaktliste.
  return !!(
    name.value &&
    eventName.value &&
    isValidStartTime.value &&
    isValidEndTime.value &&
    // Slettede oppgaver teller ikke.
    visibleResources(resources.value).length &&
    hasValidResourceTimes.value &&
    // Samme regler som backend (minst 1, ikke samme kompetanse to ganger).
    areValidCompetencyRequirements(competencyRequirements.value)
  );
});

async function saveTemplate() {
  // Lagre-knappen er deaktivert uten canSave; sjekken her er et ekstra vern
  // mot RangeError i mapToModel.
  if (!canSave.value) return;
  const model = mapToModel();
  emit("save", model);
}

function mapToModel(): TemplateFormModel {
  const model: TemplateFormModel = {
    id: props.modelValue.id,
    name: name.value,
    eventName: eventName.value,
    // Malen lagrer bare klokkeslettet ("HH:mm").
    startTime: toTimeWire(startTime.value),
    endTime: toTimeWire(endTime.value),
    resourceTemplates: toResourceTemplateRequests(resources.value),
    competencyRequirements: toCompetencyRequirementRequests(
      competencyRequirements.value
    ),
  };
  return model;
}

const showingDelete = ref<boolean | null>(null);
function confirmDeleteEvent() {
  showingDelete.value = true;
}

function deleteTemplate() {
  // Sletting trenger kun id, så ugyldige tider i skjemaet stopper den ikke.
  showingDelete.value = false;
  emit("delete", { id: props.modelValue.id });
}
</script>
