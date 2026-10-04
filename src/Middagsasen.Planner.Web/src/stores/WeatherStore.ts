import { defineStore } from "pinia";
import { api } from "boot/axios";
import { lastHours, toUtcWire } from "src/shared/time";
import type { LocationMeasurementResponse } from "src/types";

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
    // Feil kastes videre; siden viser varselet.
    async getLocations(): Promise<void> {
      const range = lastHours(2);
      const start = toUtcWire(range.start);
      const end = toUtcWire(range.end);
      const response = await api.get<LocationMeasurementResponse[]>(
        `/api/weather?start=${encodeURIComponent(
          start
        )}&end=${encodeURIComponent(end)}`
      );
      this.locations = response.data;
    },
  },
});
