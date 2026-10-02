import { defineStore } from "pinia";
import { api } from "boot/axios";
import { UTCDate } from "@date-fns/utc";
import { formatISO, subHours } from "date-fns";
import type { LocationMeasurementResponse } from "src/types/weather";

interface WeatherState {
  locations: LocationMeasurementResponse[];
}

export const useWeatherStore = defineStore("weather", {
  state: (): WeatherState => ({
    locations: [],
  }),
  // getters: {
  //   doubleCount: (state) => state.counter * 2,
  // },
  actions: {
    async getLocations(): Promise<void> {
      try {
        const now = new UTCDate();
        const start = formatISO(subHours(now, 2));
        const end = formatISO(now);
        const response = await api.get<LocationMeasurementResponse[]>(
          `/api/weather?start=${encodeURIComponent(
            start
          )}&end=${encodeURIComponent(end)}`
        );
        this.locations = response.data;
      } catch (error) {
        console.log(error);
      }
    },
  },
});
