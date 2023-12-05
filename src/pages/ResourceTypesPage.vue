<template>
  <q-page padding>
    <q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="arrow_back"
          @click="$router.go(-1)"
        ></q-btn>
        <q-toolbar-title>Vakttype</q-toolbar-title>
        <q-space></q-space>
        <q-btn
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
        ></q-btn>
      </q-toolbar>
    </q-header>
    <q-list separator>
      <q-item
        v-for="resourceType in eventStore.resourceTypes"
        :key="resourceType.id"
      >
        <q-item-section>
          <q-item-label>{{ resourceType.name }} </q-item-label
          ><q-item-label caption
            >{{ resourceType.defaultStaff }}
            {{ resourceType.defaultStaff === 1 ? "vakt" : "vakter" }}
          </q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-btn
            round
            flat
            icon="edit"
            @click="editResourceType(resourceType)"
          ></q-btn>
        </q-item-section>
      </q-item> </q-list
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
          @click="newResourceType"
      /></q-toolbar>
    </q-footer>
    <q-dialog v-model="showingEdit">
      <q-card class="full-width">
        <q-card-section class="row"
          ><span>{{
            !selectedResource.id ? "Ny vakttype" : "Endre vakttype"
          }}</span
          ><q-space></q-space>
          <q-btn
            v-if="selectedResource.id"
            color="negative"
            round
            flat
            icon="delete"
          ></q-btn
        ></q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input
            autofocus
            outlined
            label="Navn"
            v-model="selectedResource.name"
          ></q-input>
          <q-input
            outlined
            label="Antall"
            v-model="selectedResource.defaultStaff"
            suffix="stk"
            @focus="(event) => event.target.select()"
          ></q-input>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Avbryt" no-caps v-close-popup></q-btn>
          <q-btn
            unelevated
            label="Lagre"
            color="primary"
            @click="saveResource"
            no-caps
          ></q-btn
        ></q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import { useEventStore } from "stores/EventStore";
import { useUserStore } from "stores/UserStore";

const router = useRouter();
const eventStore = useEventStore();

const showingEdit = ref(false);

onMounted(() => {
  eventStore.getResourceTypes();
});

const selectedResource = ref(null);
function newResourceType() {
  selectedResource.value = emptyResource();
  showingEdit.value = true;
}

function editResourceType(resourceType) {
  selectedResource.value = Object.assign({}, resourceType);
  showingEdit.value = true;
}

function saveResource() {
  if (selectedResource.value.id) {
    eventStore.updateResourceType(selectedResource.value);
  } else {
    eventStore.createResourceType(selectedResource.value);
  }
  showingEdit.value = false;
}

function emptyResource() {
  return {
    id: null,
    name: null,
    defaultStaff: 1,
  };
}
</script>
