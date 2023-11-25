<template>
  <q-page padding>
    <q-header
      ><q-toolbar
        ><q-toolbar-title>Vaktliste</q-toolbar-title></q-toolbar
      ></q-header
    >
    <div class="q-gutter-sm">
      <q-input outlined label="Navn"></q-input>
      <q-input outlined label="Dato"></q-input>
      <q-input outlined label="Start"></q-input>
      <q-input outlined label="Slutt"></q-input>
      <q-list>
        <q-item>
          <q-item-section>
            <q-item-label>07.00 19.00</q-item-label>
            <q-item-label>Heis voksen: 2</q-item-label></q-item-section
          >
          <q-item-section side
            ><q-btn flat round icon="delete"></q-btn
          ></q-item-section>
        </q-item>
      </q-list>
      <div class="text-right">
        <q-btn
          round
          color="primary"
          icon="add"
          @click="showingEdit = true"
        ></q-btn>
      </div>
      <q-dialog v-model="showingEdit">
        <q-card>
          <q-card-section class="q-gutter-md">
            <q-select
              label="Vakt"
              outlined
              :options="resourceTypes"
              option-label="name"
              option-value="resourceTypeId"
            ></q-select>
            <q-input
              outlined
              label="Antall"
              suffix="stk"
              type="number"
            ></q-input>
            <q-input outlined label="Start"></q-input>
            <q-input outlined label="Slutt"></q-input
          ></q-card-section>
          <q-separator></q-separator>
          <q-card-actions>
            <q-btn no-caps label="Avbryt" @click="showingEdit = false"></q-btn>
            <q-space></q-space>
            <q-btn no-caps label="Lagre" @click="showingEdit = false"></q-btn>
          </q-card-actions>
        </q-card>
      </q-dialog>
    </div>
  </q-page>
</template>

<script setup>
import { computed, inject, onMounted, ref, reactive } from "vue";
import { useQuasar } from "quasar";
import { useEventStore } from "stores/EventStore";
import { formatISO, addDays, isBefore, isAfter } from "date-fns";
import { useI18n } from "vue-i18n";

const loading = false;
const $q = useQuasar();
const eventStore = useEventStore();

onMounted(() => {
  eventStore.getResourceTypes();
});

const resourceTypes = computed(() => eventStore.resourceTypes);

const showingEdit = ref(false);
</script>
