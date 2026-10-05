<template>
  <q-page padding>
    <!-- Ugyldig id i URL-en (/edit/abc) ville ellers gitt et tomt «ny vaktliste»-skjema. -->
    <div v-if="invalidId" class="q-mt-xl text-center">
      <p>Fant ikke vaktlista.</p>
      <q-btn
        no-caps
        unelevated
        color="primary"
        label="Til kalenderen"
        to="/"
      ></q-btn>
    </div>
    <!-- key: ny innlasting når ruten bytter vaktliste eller dato. -->
    <EventForm
      v-else
      :key="$route.fullPath"
      flat
      bordered
      :id="eventId"
      :date="props.date"
      @cancel="$router.go(-1)"
      @saved="(model) => goToDay(toDayKey(model.startTime))"
      @deleted="goToDay"
    ></EventForm>
  </q-page>
</template>

<script setup lang="ts">
// Rutene /create/:date og /edit/:id: skall rundt EventForm (samme skjema som
// dialogen i IndexPage). Etter lagring og sletting går siden til dagen.
import { computed } from "vue";
import { useRouter } from "vue-router";
import EventForm from "@/components/EventForm.vue";
import { toDayKey, today } from "@/shared/time";

const $router = useRouter();

const props = withDefaults(
  defineProps<{
    date?: string;
    id?: string | null;
  }>(),
  {
    date: () => today(),
    id: null,
  }
);

// Route-param som tall; null ved ny vaktliste eller ugyldig id.
const eventId = computed(() =>
  props.id !== null && /^[1-9]\d*$/.test(props.id) ? Number(props.id) : null
);
const invalidId = computed(() => props.id !== null && eventId.value === null);

function goToDay(day: string) {
  // Tom dag-nøkkel (ugyldig dato) gir forsiden, som sender til i dag.
  void $router.push(day ? `/day/${day}` : "/");
}
</script>
