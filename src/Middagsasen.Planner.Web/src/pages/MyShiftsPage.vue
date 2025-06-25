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
    <q-expansion-item
      v-for="season in seasons"
      :key="season.id"
      expand-separator
      default-opened
      :label="season.name"
      class="q-item__label"
      :caption="countShiftsForSeason(season)"
       >
      <q-list role="list" separator>
        <q-item
          separator
          v-for="shift in season.shifts"
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
            <q-item-label>{{
              shift.formattedTime
            }}</q-item-label></q-item-section
          >
        </q-item>
      </q-list>
      <q-inner-loading :showing="loading">
        <q-spinner size="3em" color="primary"></q-spinner>
      </q-inner-loading>
    </q-expansion-item>
  </q-page>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { useQuasar } from "quasar";
import { useRouter } from "vue-router";
import { api } from "src/boot/axios";
import { parse, parseISO, format } from "date-fns";
import { id, nb } from "date-fns/locale";

const emit = defineEmits(["toggle-right"]);
const loading = ref(false);
const $q = useQuasar();
const $router = useRouter();

const shifts = ref([]);

const seasons = ref([
  {
    id: "id1",
    name: "2024/2025",
    startDate: "2024-10-01",
    endDate: "2025-03-31"
  },
  {
    id: "id2",
    name: "2025/2026",
    startDate: "2025-10-01",
    endDate: "2026-03-31"
  },
  {
    id: "id2",
    name: "2026/2027",
    startDate: "2026-10-01",
    endDate: "2027-03-31"

  }
])

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
        formattedMonth: format(startDate, "MM"),
        formattedMonthAndYear: format(startDate, "yyyy.MM"),
        formattedTime: `${startTime}-${endTime}`,
        resourceName: s.resourceName,
        commen: s.comment,
      };
    });
        seasons.value.forEach((season) => {
      const startDate = new Date(season.startDate);
      const endDate = new Date(season.endDate);

      season.shifts = shifts.value.filter((shift) => {
        const shiftDate = new Date(shift.startDate);
        return shiftDate >= startDate && shiftDate <= endDate;
      });
    });
    
  } catch (error) {
  } finally {
    loading.value = false;
  }

});

function countShiftsForSeason(season) {
  if (!season || !season.startDate || !season.endDate) return 0;

  return shifts.value.filter((shift) => {
    const shiftDate = new Date(shift.startDate);
    return (
      shiftDate >= new Date(season.startDate) &&
      shiftDate <= new Date(season.endDate)
    );
  }).length;
}


</script>
