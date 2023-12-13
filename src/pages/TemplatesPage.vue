<template>
  <q-page padding
    ><q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="arrow_back"
          @click="$router.go(-1)"
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Maler</q-toolbar-title>
        <q-space></q-space>
        <q-btn
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
          title="Din brukerinfo"
        ></q-btn>
      </q-toolbar>
    </q-header>

    <q-list separator>
      <q-item v-for="template in templates" :key="template.id">
        <q-item-section>
          <q-item-label lines="1">{{ template.name }}</q-item-label>
          <q-item-label caption lines="1"
            >{{ template.eventName }}
            {{ formatStartEndTime(template) }}</q-item-label
          >
        </q-item-section>

        <q-item-section side>
          <q-btn
            flat
            round
            icon="edit"
            @click="editTemplate(template)"
            title="Endre mal"
          ></q-btn>
        </q-item-section>
      </q-item>
    </q-list>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner> </q-inner-loading
    ><q-footer>
      <q-toolbar>
        <q-space></q-space>
        <q-btn
          fab
          unelevated
          padding="md"
          class="q-ma-xs"
          icon="add"
          color="accent"
          text-color="blue-grey-9"
          @click="newTemplate"
          title="Legg til ny mal"
      /></q-toolbar>
    </q-footer>
    <q-dialog v-model="showingEditDialog" persistent maximized>
      <TemplateForm
        v-model="selectedTemplate"
        :resource-types="resourceTypes"
        :loading="savingTemplate"
        @cancel="showingEditDialog = false"
        @save="saveTemplate"
        @delete="deleteTemplate"
      ></TemplateForm>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useRouter } from "vue-router";
import { useEventStore } from "src/stores/EventStore";
import { parseISO, format } from "date-fns";
import TemplateForm from "components/TemplateForm.vue";

const emit = defineEmits(["toggle-right"]);
const loading = ref(false);
const $q = useQuasar();
const $router = useRouter();
const eventStore = useEventStore();

onMounted(async () => {
  eventStore.getResourceTypes();
  await eventStore.getTemplates();
});

const templates = computed(() => eventStore.templates);
const resourceTypes = computed(() => eventStore.resourceTypes);

const selectedTemplate = ref(null);
const showingEditDialog = ref(false);
function newTemplate() {
  selectedTemplate.value = {
    id: 0,
    name: null,
    eventName: "Åpningstid",
    startTime: "2023-12-01T10:00",
    endTime: "2023-12-01T17:00",
    resourceTemplates: [],
  };
  showingEditDialog.value = true;
}

function editTemplate(template) {
  selectedTemplate.value = template;
  showingEditDialog.value = true;
}

function formatStartEndTime(template) {
  const start = format(parseISO(template.startTime), "HH:mm");
  const end = format(parseISO(template.endTime), "HH:mm");
  return `${start}-${end}`;
}

const savingTemplate = ref(false);
async function saveTemplate(model) {
  try {
    savingTemplate.value = true;
    if (model.id) {
      await eventStore.updateTemplate(model);
      $q.notify({
        message: "Endringer i malen er lagret.",
      });
    } else {
      await eventStore.createTemplate(model);
      $q.notify({
        message: "Malen er lagt til.",
      });
    }
    showingEditDialog.value = false;
  } catch (error) {
    $q.notify({
      message: "Klarte ikke å lagre.",
    });
  } finally {
    savingTemplate.value = false;
  }
}

async function deleteTemplate(model) {
  try {
    savingTemplate.value = true;
    await eventStore.deleteTemplate(model);
    $q.notify({
      message: "Malen er slettet.",
    });
    showingEditDialog.value = false;
  } catch (error) {
  } finally {
    savingTemplate.value = false;
  }
}
</script>
