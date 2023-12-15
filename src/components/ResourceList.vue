<template>
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
          ><q-btn flat round icon="edit" @click="editResource(resource)"></q-btn
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
    <q-dialog v-model="showingEdit">
      <ResourceForm
        :model-value="selectedResource"
        :resource-types="props.resourceTypes"
        @cancel="showingEdit = false"
        @save="saveResource"
      ></ResourceForm>
    </q-dialog>
  </q-card>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { parseISO, format, isValid, addMinutes, parse } from "date-fns";
import ResourceForm from "components/ResourceForm.vue";

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => [],
  },
  resourceTypes: {
    type: Array,
    require: true,
  },
  startTime: {
    type: String,
    require: true,
  },
  endTime: {
    type: String,
    require: true,
  },
});

const emit = defineEmits(["update:model-value"]);

const visibleResources = computed(() =>
  props.modelValue.filter((r) => !r.isDeleted)
);

const selectedResource = ref(null);

const showingEdit = ref(false);

function addResource() {
  selectedResource.value = {
    resourceType: null,
    startTime: format(addMinutes(toDateTime(props.startTime), -30), "HH:mm"),
    endTime: format(addMinutes(toDateTime(props.endTime), 30), "HH:mm"),
    minimumStaff: 1,
    isNew: true,
  };
  showingEdit.value = true;
}

function editResource(resource) {
  selectedResource.value = resource;
  showingEdit.value = true;
}

function toDateTime(time) {
  const datetime = parse(time, "HH:mm", new Date());
  return datetime;
}

function saveResource(model) {
  if (model?.isNew) {
    let resources = [...props.modelValue];
    resources.push({
      resourceType: model.resourceType,
      startTime: model.startTime,
      endTime: model.endTime,
      minimumStaff: model.minimumStaff,
      isDeleted: false,
    });
    emit("update:model-value", resources);
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
</script>
