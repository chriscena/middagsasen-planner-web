<template>
  <q-card>
    <q-card-section class="row text-h6"
      ><q-btn icon="close" round flat dense @click="emit('close')"></q-btn>
      <div>Superfrivillige! 🏆</div></q-card-section
    >
    <q-separator></q-separator>
    <q-list role="list" separator>
      <q-item v-if="!hallOfFamers.length && !loading">
        <q-item-section class="text-center"
          ><q-item-label caption
            >Ingen vakter gjennomført ennå</q-item-label
          ></q-item-section
        >
      </q-item>
      <q-item dense v-if="hallOfFamers.length">
        <q-item-section avatar>#</q-item-section>
        <q-item-section
          ><q-item-label caption>Navn</q-item-label></q-item-section
        >
        <q-item-section side
          ><q-item-label caption>Vakter</q-item-label></q-item-section
        >
      </q-item>
      <q-item
        v-for="(hallOfFamer, index) in hallOfFamers"
        :key="hallOfFamer.id"
      >
        <q-item-section avatar class="text-subtitle1">
          {{ rank(hallOfFamer.shifts, index) }}
        </q-item-section>
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
    lastRank = 0;
    const response = await api.get("/api/halloffame");
    hallOfFamers.value = response.data?.hallOfFamers;
  } catch (error) {
  } finally {
    loading.value = false;
  }
});

let lastRank = 0;
function rank(shifts, index) {
  if (shifts !== lastRank) {
    lastRank = shifts;
    if (index === 0) return "🥇";
    if (index === 1) return "🥈";
    if (index === 2) return "🥉";
    return index + 1;
  }
  return "";
}
</script>
