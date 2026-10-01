import { defineStore } from "pinia";
import { api } from "boot/axios";

export const useSeasonStore = defineStore("seasons", {
  state: () => ({
    seasons: [],
    loaded: false,
    // Pågående kall, slik at samtidige kall deler samme forespørsel.
    loadPromise: null,
  }),
  getters: {
    currentSeason: (state) =>
      state.seasons.find((s) => s.isCurrent) ?? state.seasons[0] ?? null,
  },
  actions: {
    async getSeasons() {
      if (this.loaded) return this.seasons;
      if (!this.loadPromise) {
        this.loadPromise = api
          .get("/api/Seasons")
          .then((response) => {
            this.seasons = response.data;
            this.loaded = true;
            return this.seasons;
          })
          .finally(() => {
            this.loadPromise = null;
          });
      }
      return this.loadPromise;
    },
  },
});
