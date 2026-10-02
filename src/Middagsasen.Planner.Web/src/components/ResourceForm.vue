<template>
  <q-card class="full-width">
    <q-card-section class="row">
      <div class="text-h6">Vakt</div>
      <q-space></q-space>
      <q-btn
        v-if="!props.modelValue.isNew"
        color="negative"
        flat
        round
        icon="delete"
        @click="deleteResource"
      ></q-btn>
    </q-card-section>
    <q-card-section class="q-gutter-md">
      <q-select
        autofocus
        label="Vakt"
        outlined
        :options="props.resourceTypes"
        option-label="name"
        option-value="resourceTypeId"
        v-model="resourceType"
        @update:model-value="resourceTypeChanged"
      ></q-select>
      <q-input
        outlined
        @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
        label="Minste bemanning"
        suffix="stk"
        step="1"
        type="number"
        v-model="minimumStaff"
      ></q-input>

      <q-input
        outlined
        label="Start"
        mask="##:##"
        placeholder="TT:MM"
        @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
        v-model="startTime"
      >
        <template v-slot:append>
          <q-icon name="access_time" class="cursor-pointer">
            <q-popup-proxy transition-show="scale" transition-hide="scale">
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
        @focus="(event) => (event.target as HTMLInputElement | null)?.select?.()"
      >
        <template v-slot:append>
          <q-icon name="access_time" class="cursor-pointer">
            <q-popup-proxy transition-show="scale" transition-hide="scale">
              <q-time v-model="endTime" format24h mask="HH:mm">
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
        @click="emit('cancel')"
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
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import type { ResourceTypeResponse } from "src/types";

// Skjemamodell for en vakt (ressurs) i ResourceList/EventForm/TemplateForm.
// Ikke en DTO: tidene er "HH:mm", og resourceType er hele objektet.
export interface ResourceFormModel {
  // Stabil nøkkel for `:key` i lister (se newClientKey). Sendes ikke til API-et.
  clientKey: string;
  id?: number | undefined;
  eventId?: number | undefined;
  resourceType: ResourceTypeResponse | null;
  // q-input type="number" sender verdien som string når brukeren skriver.
  minimumStaff: number | string | null;
  startTime: string | null;
  endTime: string | null;
  isDeleted?: boolean | undefined;
  isNew?: boolean | undefined;
}

const emit = defineEmits<{
  "update:model-value": [value: ResourceFormModel];
  cancel: [];
  save: [value: ResourceFormModel];
}>();

const props = defineProps<{
  modelValue: ResourceFormModel;
  resourceTypes: ResourceTypeResponse[];
}>();

const resourceType = ref<ResourceTypeResponse | null>(null);
const minimumStaff = ref<number | string | null>(1);
const startTime = ref<string | null>(null);
const endTime = ref<string | null>(null);
const isDeleted = ref<boolean | undefined>(false);
onMounted(() => {
  resourceType.value = props.modelValue.resourceType;
  minimumStaff.value = props.modelValue.minimumStaff;
  startTime.value = props.modelValue.startTime;
  endTime.value = props.modelValue.endTime;
  isDeleted.value = props.modelValue.isDeleted;
});

function resourceTypeChanged(newValue: ResourceTypeResponse | null) {
  if (newValue && newValue.defaultStaff) {
    minimumStaff.value = newValue.defaultStaff;
  }
}

const canAdd = computed(() => {
  return !!(
    resourceType.value &&
    startTime.value &&
    endTime.value &&
    // Number() gir samme sammenligning som JS-ens implisitte konvertering.
    Number(minimumStaff.value) > 0
  );
});

function saveResource() {
  const model = mapToModel();
  emit("update:model-value", model);
  emit("save", model);
}

function mapToModel(): ResourceFormModel {
  return {
    id: props.modelValue.id,
    clientKey: props.modelValue.clientKey,
    resourceType: resourceType.value,
    minimumStaff: minimumStaff.value,
    startTime: startTime.value,
    endTime: endTime.value,
    isDeleted: isDeleted.value,
    isNew: props.modelValue.isNew,
  };
}

function deleteResource() {
  isDeleted.value = true;
  saveResource();
}
</script>
