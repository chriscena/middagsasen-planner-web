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
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Mine vakter</q-toolbar-title>
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

    <q-list role="list" separator>
      <q-item
        separator
        v-for="shift in shifts"
        :key="shift.id"
        :to="`/day/${shift.startDate}`"
        clickable
        v-ripple
      >
        <q-item-section avatar class="items-center">
          <q-item-label caption>{{ shift.formattedDay }}</q-item-label>
          <q-item-label>{{ shift.formattedStartDate }}</q-item-label>
          <q-item-label caption>{{ shift.formattedYear }}</q-item-label>
        </q-item-section>

        <q-item-section>
          <q-item-label>{{ shift.resourceName }}</q-item-label>
          <q-item-label caption>{{ shift.comment }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-item-label>{{ shift.formattedTime }}</q-item-label></q-item-section
        >
      </q-item>
    </q-list>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-page>
</template>

<script setup>
import { onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useRouter } from "vue-router";
import { api } from "src/boot/axios";
import { parse, parseISO, format } from "date-fns";
import { nb } from "date-fns/locale";

const emit = defineEmits(["toggle-right"]);
const loading = ref(false);
const $q = useQuasar();
const $router = useRouter();

const shifts = ref([]);

onMounted(async () => {
  try {
    loading.value = true;
    const response = await api.get("/api/me/shifts");
    const newShifts = response.data;
    shifts.value = newShifts.map((s) => {
      const startDate = parse(s.startDate, "yyyy-MM-dd", new Date());
      const startTime = format(parseISO(s.startTime), "HH:mm");
      const endTime = format(parseISO(s.endTime), "HH:mm");
      return {
        id: s.id,
        startDate: s.startDate,
        formattedDay: format(startDate, "EEE", { locale: nb }),
        formattedStartDate: format(startDate, "dd.MM"),
        formattedYear: format(startDate, "yyyy"),
        formattedTime: `${startTime}-${endTime}`,
        resourceName: s.resourceName,
        commen: s.comment,
      };
    });
  } catch (error) {
  } finally {
    loading.value = false;
  }
});
</script>
