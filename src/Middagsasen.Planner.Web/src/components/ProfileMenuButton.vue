<template>
  <q-btn
    dense
    flat
    round
    icon="person"
    title="Din brukerinfo"
    @click="emit('click')"
  >
    <q-badge
      v-if="unacknowledgedCount > 0"
      floating
      rounded
      color="orange"
      :label="unacknowledgedCount"
    ></q-badge>
  </q-btn>
</template>

<script setup lang="ts">
// Profilknappen i toppmenyen på alle sider. Åpner høyremenyen (click) og
// viser antall uleste instrukser som gjelder brukeren, så det er synlig uten
// å åpne menyen. Telleren holdes oppdatert av MainLayout og kvitteringene.
import { computed } from "vue";
import { useInstructionStore } from "@/stores/InstructionStore";

const emit = defineEmits<{ click: [] }>();

const instructionStore = useInstructionStore();
const unacknowledgedCount = computed(
  () => instructionStore.unacknowledgedCount
);
</script>
