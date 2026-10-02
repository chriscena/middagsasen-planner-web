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
        <Line :data="measurement.data" :options="measurement.options"></Line>
      </q-card-section>
    </q-card>
    <q-inner-loading :showing="loading">
      <q-spinner size="3em" color="primary"></q-spinner>
    </q-inner-loading>
  </q-page>
</template>

<script setup lang="ts">
import { onMounted, ref, computed } from "vue";
import { useWeatherStore } from "src/stores/WeatherStore";
import {
  Chart as ChartJS,
  TimeScale,
  LinearScale,
  PointElement,
  LineElement,
  Colors,
  Title,
  Tooltip,
} from "chart.js";
import type { ChartData, ChartOptions, Point } from "chart.js";
import "chartjs-adapter-date-fns";
import { Line } from "vue-chartjs";

const loading = ref(false);
ChartJS.register(
  TimeScale,
  LinearScale,
  PointElement,
  LineElement,
  Colors,
  Title,
  Tooltip
);

const weatherStore = useWeatherStore();

interface MeasurementViewModel {
  measurementName: string;
  data: ChartData<"line", (number | Point | null)[]>;
  options: ChartOptions<"line">;
}

interface LocationViewModel {
  locationName: string;
  measurements: MeasurementViewModel[];
}

const locations = computed((): LocationViewModel[] =>
  weatherStore.locations.map((l) => {
    return {
      locationName: l.locationName,
      measurements: l.measurements.map((m) => {
        return {
          measurementName: m.measurementName,
          data: {
            datasets: [
              {
                //label: m.measurementName,
                data: m.values.map((v) => {
                  // Tidsskalaen godtar Date som x, men Chart.js' Point-type
                  // krever number.
                  return {
                    y: v.value,
                    x: new Date(v.measuredTime),
                  } as unknown as Point;
                }),
              },
            ],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            resizeDelay: 200,
            scales: {
              x: {
                type: "time",
                time: {
                  unit: "minute",
                  displayFormats: {
                    minute: "HH:mm",
                  },
                },
              },
              // y: {
              //   title: {
              //     display: true,
              //     text: m.unit,
              //   },
              // },
            },
            plugins: {
              legend: {
                display: false,
              },
              title: {
                display: true,
                // lastValue er nullable i typen, men WeatherService setter den
                // alltid (values.Last() på en ikke-tom gruppe).
                text: `${m.measurementName}: ${m.lastValue!.value} ${
                  m.unit
                } kl. ${new Date(
                  m.lastValue!.measuredTime
                ).toLocaleTimeString()}`,
              },
              // subtitle: {
              //   display: true,
              //   text: `${m.lastValue.value}${m.unit} kl. ${new Date(
              //     m.lastValue.measuredTime
              //   ).toLocaleTimeString()}`,
              // },
            },
          },
        };
      }),
    };
  })
);

async function getWeatherData(): Promise<void> {
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
