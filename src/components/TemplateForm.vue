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
        <q-input
          hide-bottom-space
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
          :error="!isValidEndTime"
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
            </q-icon> </template
        ></q-input>
        <q-card bordered flat>
          <q-list separator>
            <q-item v-if="!visibleResources.length">
              <q-item-section>
                <q-item-label>Ingen vakter</q-item-label></q-item-section
              >
            </q-item>
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
          <q-separator></q-separator>
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

        <div class="q-mt-xl text-center">
          <q-btn
            v-if="modelValue.id"
            @click="confirmDeleteEvent"
            icon="delete"
            no-caps
            unelevated
            color="negative"
            label="Slett mal"
          ></q-btn>
        </div>
      </q-card-section>
      ></q-form
    >
    <q-dialog v-model="showingEdit">
      <ResourceForm
        :model-value="selectedResource"
        :resource-types="props.resourceTypes"
        @cancel="showingEdit = false"
        @save="saveResource"
      ></ResourceForm>
    </q-dialog>
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
import { useQuasar } from "quasar";
import { parseISO, format, isValid, addMinutes, parse } from "date-fns";
import { useRouter } from "vue-router";
import ResourceForm from "components/ResourceForm.vue";

const emit = defineEmits(["cancel", "save", "delete"]);
const $q = useQuasar();
const $router = useRouter();

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

const visibleResources = computed(() =>
  resources.value.filter((r) => !r.isDeleted)
);

const selectedResource = ref(null);
const showingEdit = ref(false);

function addResource() {
  selectedResource.value = {
    resourceType: null,
    startTime: startTime.value
      ? format(addMinutes(toDateTime(startTime.value), -30), "HH:mm")
      : null,
    endTime: endTime.value
      ? format(addMinutes(toDateTime(endTime.value), 30), "HH:mm")
      : null,
    minimumStaff: 1,
    isNew: true,
  };
  showingEdit.value = true;
}

function editResource(resource) {
  selectedResource.value = resource;
  showingEdit.value = true;
}

function saveResource(model) {
  if (model?.isNew) {
    resources.value.push({
      resourceType: model.resourceType,
      startTime: model.startTime,
      endTime: model.endTime,
      minimumStaff: model.minimumStaff,
      isDeleted: false,
    });
  } else {
    selectedResource.value.resourceType = model.resourceType;
    selectedResource.value.resourceType = model.resourceType;
    selectedResource.value.startTime = model.startTime;
    selectedResource.value.endTime = model.endTime;
    selectedResource.value.minimumStaff = model.minimumStaff;
    selectedResource.value.isDeleted = model.isDeleted;
  }
  showingEdit.value = false;
}

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
