import { defineStore } from "pinia";
import { api } from "@/boot/axios";
import type { SeasonResponse } from "@/types";

interface SeasonState {
  seasons: SeasonResponse[];
  loaded: boolean;
  loadPromise: Promise<SeasonResponse[]> | null;
}

export const useSeasonStore = defineStore("seasons", {
  state: (): SeasonState => ({
    seasons: [],
    loaded: false,
    // Pågående kall, slik at samtidige kall deler samme forespørsel.
    loadPromise: null,
  }),
  getters: {
    currentSeason: (state): SeasonResponse | null =>
      state.seasons.find((s) => s.isCurrent) ?? state.seasons[0] ?? null,
  },
  actions: {
    async getSeasons(): Promise<SeasonResponse[]> {
      if (this.loaded) return this.seasons;
      if (!this.loadPromise) {
        this.loadPromise = api
          .get<SeasonResponse[]>("/api/Seasons")
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
