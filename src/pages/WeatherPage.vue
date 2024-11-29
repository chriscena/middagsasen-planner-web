<template>
  <q-page padding
    ><q-header>
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="arrow_back"
          @click="$router.go(-1)"
          title="Tilbake"
        ></q-btn>
        <q-toolbar-title>Værdata</q-toolbar-title>
        <q-space></q-space>
        <!-- <q-btn
          dense
          flat
          round
          icon="person"
          @click="emit('toggle-right')"
          title="Din brukerinfo"
        ></q-btn> -->
        <q-btn
          dense
          flat
          round
          icon="refresh"
          @click="getWeatherData"
          title="Oppfrisk værdata"
          :loading="loading"
        ></q-btn>
      </q-toolbar>
    </q-header>
    <q-card
      class="q-mb-sm"
      v-for="location in locations"
      :key="location.locationName"
    >
      <q-card-section class="text-h6">{{
        location.locationName
      }}</q-card-section>
      <q-card-section
        v-for="measurement in location.measurements"
        :key="measurement.measurementName"
      >
        <Line
          :data="{
            datasets: [
              {
                label: measurement.measurementName,
                data: measurement.values.map((v) => {
                  return { y: v.value, x: new Date(v.measuredTime) };
                }),
              },
            ],
          }"
          :options="{
            responsive: true,
            maintainAspectRatio: false,
            resizeDelay: 200,
            scales: {
              x: {
                type: 'time',
                time: {
                  unit: 'minute',
                  displayFormats: {
                    minute: 'HH:mm',
                  },
                },
              },
            },
          }"
        ></Line>
      </q-card-section>
    </q-card>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-page>
</template>

<script setup>
import { onMounted, ref, computed } from "vue";
import { useWeatherStore } from "src/stores/WeatherStore";
import {
  Chart as ChartJS,
  TimeScale,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Colors,
  Title,
  Tooltip,
  Legend,
} from "chart.js";
import "chartjs-adapter-date-fns";
import { Line } from "vue-chartjs";

const loading = ref(false);
ChartJS.register(
  TimeScale,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Colors,
  Title,
  Tooltip,
  Legend
);

var weatherStore = useWeatherStore();

const locations = computed(() => weatherStore.locations);

async function getWeatherData() {
  try {
    loading.value = true;
    await weatherStore.getLocations();
  } catch (error) {
    console.log(error);
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  await getWeatherData();
});
</script>
