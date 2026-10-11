<template>
  <q-btn
    dense
    flat
    round
    icon="person"
    :title="title"
    :aria-label="title"
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
import { profileMenuTitle } from "@/shared/instructionList";

const emit = defineEmits<{ click: [] }>();

const instructionStore = useInstructionStore();
const unacknowledgedCount = computed(
  () => instructionStore.unacknowledgedCount
);
// Tallet i badgen er bare visuelt; tittelen bærer det for skjermlesere.
const title = computed(() => profileMenuTitle(unacknowledgedCount.value));
</script>
