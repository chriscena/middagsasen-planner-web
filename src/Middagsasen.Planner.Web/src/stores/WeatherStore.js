import { defineStore } from "pinia";
import { api } from "boot/axios";
import { UTCDate } from "@date-fns/utc";
import { formatISO, subHours } from "date-fns";

export const useWeatherStore = defineStore("weather", {
  state: () => ({
    locations: [],
  }),
  // getters: {
  //   doubleCount: (state) => state.counter * 2,
  // },
  actions: {
    async getLocations() {
      try {
        const now = new UTCDate();
        const start = formatISO(subHours(now, 2));
        const end = formatISO(now);
        const response = await api.get(
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
