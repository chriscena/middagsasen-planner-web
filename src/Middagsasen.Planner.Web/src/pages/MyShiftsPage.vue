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
      <template v-for="season in viewModel.shifts" :key="season.label">
        <q-item dense>
          <q-item-section>
            <q-item-label header
              >{{ season.label }}
              <q-badge
                :label="season.shifts.length"
                color="primary"
                align="top"
              ></q-badge
            ></q-item-label>
          </q-item-section>
        </q-item>
        <q-item
          separator
          v-for="shift in season.shifts"
          :key="shift.id"
          :to="`/day/${shift.startDate}`"
          clickable
          v-ripple
        >
          <q-item-section avatar class="items-center">
            <q-item-label caption>{{
              formatWeekday(shift.startDate)
            }}</q-item-label>
            <q-item-label>{{ formatDayMonth(shift.startDate) }}</q-item-label>
          </q-item-section>

          <q-item-section>
            <q-item-label class="ellipsis">{{
              shift.resourceName
            }}</q-item-label>
            <q-item-label caption>{{ shift.comment }}</q-item-label>
          </q-item-section>
          <q-item-section side>
            <q-item-label>{{
              formatTimeRange(shift.startTime, shift.endTime)
            }}</q-item-label></q-item-section
          >
        </q-item>
      </template>
    </q-list>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-page>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useQuasar } from "quasar";
import { useRouter } from "vue-router";
import { api } from "src/boot/axios";
import type { ShiftSeasonResponse } from "src/types";
import {
  formatDayMonth,
  formatTimeRange,
  formatWeekday,
} from "src/shared/time";

interface ViewModel {
  shifts: ShiftSeasonResponse[];
}

const emit = defineEmits<{ "toggle-right": [] }>();
const loading = ref(false);
const $q = useQuasar();
const $router = useRouter();

const viewModel = reactive<ViewModel>({
  shifts: [],
});

// startDate/startTime/endTime er nullable i UserShiftResponse (Shift.StartTime/
// EndTime er nullable i databasen). Manglende/ugyldige verdier vises som tomt
// felt (formatfunksjonene i shared/time gir tom streng).

onMounted(async () => {
  try {
    loading.value = true;
    const response = await api.get<ShiftSeasonResponse[]>("/api/me/shifts");
    viewModel.shifts = response.data;
  } catch {
    $q.notify({
      type: "negative",
      message: "Klarte ikke å hente vaktene dine 🙈",
    });
  } finally {
    loading.value = false;
  }
});
</script>
