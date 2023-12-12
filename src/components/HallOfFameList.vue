<template>
  <q-card>
    <q-card-section class="row text-h6"
      ><q-btn icon="close" round flat dense @click="emit('close')"></q-btn>
      <div>Supervakter! 🏆</div></q-card-section
    >
    <q-separator></q-separator>
    <q-list separator>
      <q-item dense>
        <q-item-section
          ><q-item-label caption>Navn</q-item-label></q-item-section
        >
        <q-item-section side
          ><q-item-label caption>Vakter</q-item-label></q-item-section
        >
      </q-item>
      <q-item v-for="hallOfFamer in hallOfFamers" :key="hallOfFamer.id">
        <q-item-section
          :class="currentUser?.id === hallOfFamer.id ? 'text-bold' : ''"
          >{{ hallOfFamer.fullName }}</q-item-section
        >
        <q-item-section side class="text-primary text-bold">
          {{ hallOfFamer.shifts }}
        </q-item-section>
      </q-item>
    </q-list>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-card>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { api } from "boot/axios";

const emit = defineEmits(["close"]);
const props = defineProps({
  currentUser: {
    type: Object,
    default: null,
  },
});

const hallOfFamers = ref([]);
const loading = ref(false);
onMounted(async () => {
  try {
    loading.value = true;
    const response = await api.get("/api/halloffame");
    hallOfFamers.value = response.data?.hallOfFamers;
  } catch (error) {
  } finally {
    loading.value = false;
  }
});
</script>
