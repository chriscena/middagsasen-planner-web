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
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
        ></q-input>
        <q-input
          hide-bottom-space
          outlined
          label="Navn på vaktliste"
          v-model="eventName"
          @focus="(event) => (event.target?.select ? event.target.select() : _)"
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

<script setup>
import { computed, onMounted, ref } from "vue";
import { parseISO, format, isValid, parse } from "date-fns";
import TimePickerInput from "components/TimePickerInput.vue";
import ResourceList from "components/ResourceList.vue";

const emit = defineEmits(["cancel", "save", "delete"]);

const props = defineProps({
  modelValue: {
    type: Object,
    require: true,
  },
  resourceTypes: {
    type: Array,
    require: true,
  },
  loading: {
    type: Boolean,
    default: false,
  },
});

onMounted(async () => {
  name.value = props.modelValue.name;
  eventName.value = props.modelValue.eventName;
  startTime.value = formatTime(props.modelValue.startTime);
  endTime.value = formatTime(props.modelValue.endTime);
  resources.value = props.modelValue.resourceTemplates.map((r) => {
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
});

const name = ref(null);
const eventName = ref(null);

const isValidStartTime = computed(() =>
  isValid(parse(startTime.value, "HH:mm", new Date()))
);
const isValidEndTime = computed(() =>
  isValid(parse(endTime.value, "HH:mm", new Date()))
);

function toDateTime(time) {
  const datetime = parse(time, "HH:mm", new Date());
  return datetime;
}

const startTime = ref("10:00");
const endTime = ref("17:00");
const resources = ref([]);

const canSave = computed(() => {
  return !!(
    name.value &&
    startTime.value &&
    endTime.value &&
    resources.value.length
  );
});

function formatTime(isoDateTime) {
  if (isoDateTime instanceof Date) return format(isoDateTime, "HH:mm");
  return format(parseISO(isoDateTime), "HH:mm");
}

async function saveTemplate() {
  const model = mapToModel();
  emit("save", model);
}

function mapToModel() {
  const model = {
    id: props.modelValue.id,
    name: name.value,
    eventName: eventName.value,
    startTime: formatDateTime(toDateTime(startTime.value)),
    endTime: formatDateTime(toDateTime(endTime.value)),
    resourceTemplates: resources.value.map((r) => {
      return {
        id: r.id,
        resourceTypeId: r.resourceType.id,
        startTime: formatDateTime(toDateTime(r.startTime)),
        endTime: formatDateTime(toDateTime(r.endTime)),
        minimumStaff: r.minimumStaff,
        isDeleted: r.isDeleted,
      };
    }),
  };
  return model;
}

const showingDelete = ref(null);
function confirmDeleteEvent() {
  showingDelete.value = true;
}

function deleteTemplate() {
  const model = mapToModel();
  showingDelete.value = false;
  emit("delete", model);
}

function formatDateTime(date) {
  return format(date, "yyyy'-'MM'-'dd'T'HH':'mm", new Date());
}
</script>
