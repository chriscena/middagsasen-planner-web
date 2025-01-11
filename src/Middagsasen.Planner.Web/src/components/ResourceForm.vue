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
        @focus="(event) => (event.target?.select ? event.target.select() : _)"
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
        @focus="(event) => (event.target?.select ? event.target.select() : _)"
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
        @focus="(event) => (event.target?.select ? event.target.select() : _)"
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

<script setup>
import { computed, onMounted, ref } from "vue";

const emit = defineEmits(["update:model-value", "cancel", "save"]);

const props = defineProps({
  modelValue: {
    type: Object,
    require: true,
  },
  resourceTypes: {
    type: Array,
    require: true,
  },
});

const resourceType = ref(null);
const minimumStaff = ref(1);
const startTime = ref(null);
const endTime = ref(null);
const isDeleted = ref(false);
onMounted(() => {
  resourceType.value = props.modelValue.resourceType;
  minimumStaff.value = props.modelValue.minimumStaff;
  startTime.value = props.modelValue.startTime;
  endTime.value = props.modelValue.endTime;
  isDeleted.value = props.modelValue.isDeleted;
});

function resourceTypeChanged(newValue) {
  if (newValue && newValue.defaultStaff) {
    minimumStaff.value = newValue.defaultStaff;
  }
}

const canAdd = computed(() => {
  return !!(
    resourceType.value &&
    startTime.value &&
    endTime.value &&
    minimumStaff.value > 0
  );
});

function saveResource() {
  const model = mapToModel();
  emit("update:model-value", model);
  emit("save", model);
}

function mapToModel() {
  return {
    id: props.modelValue.id,
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
