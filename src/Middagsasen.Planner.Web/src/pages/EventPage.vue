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
      @cancel="goBack"
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

defineEmits<{ "toggle-right": [] }>();
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
const eventId = computed(() => {
  if (props.id === null || !/^[1-9]\d*$/.test(props.id)) return null;
  const id = Number(props.id);
  return Number.isSafeInteger(id) ? id : null;
});
const invalidId = computed(() => props.id !== null && eventId.value === null);

// Dagen er aldri tom: den kommer fra en lagret (validert) eller lastet
// vaktliste. Feiler navigeringen, forblir skjemaet låst (se `finished` i
// EventForm), så en ny lagring ikke gir duplikat eller 409.
function goToDay(day: string) {
  void $router.push(`/day/${day}`);
}

// Tilbake hvis forrige side er i appen (vue-router 4 legger forrige sti i
// history.state.back, null ved første side), ellers til kalenderen. Ellers
// ville Avbryt forlate appen når siden er åpnet direkte via URL.
function goBack() {
  const state: unknown = window.history.state;
  const back =
    typeof state === "object" && state !== null && "back" in state
      ? state.back
      : null;
  if (back) $router.back();
  else void $router.push("/");
}
</script>
