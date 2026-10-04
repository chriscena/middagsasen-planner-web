import { vi, describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { SeasonResponse } from "@/types";

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
}));

vi.mock("@/boot/axios", () => ({
  api: mockApi,
}));

import { useSeasonStore } from "@/stores/SeasonStore";

const seasons: SeasonResponse[] = [
  { startYear: 2026, label: "2026/2027", isCurrent: true },
  { startYear: 2025, label: "2025/2026", isCurrent: false },
];

describe("SeasonStore", () => {
  let store: ReturnType<typeof useSeasonStore>;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useSeasonStore();
    vi.clearAllMocks();
  });

  describe("getSeasons", () => {
    it("fetches seasons from /api/Seasons and populates state", async () => {
      mockApi.get.mockResolvedValue({ data: seasons });

      const result = await store.getSeasons();

      expect(mockApi.get).toHaveBeenCalledWith("/api/Seasons");
      expect(result).toEqual(seasons);
      expect(store.seasons).toEqual(seasons);
    });

    it("caches result so sequential calls only hit the api once", async () => {
      mockApi.get.mockResolvedValue({ data: seasons });

      await store.getSeasons();
      const second = await store.getSeasons();

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(second).toEqual(seasons);
    });

    it("shares one request between concurrent calls", async () => {
      mockApi.get.mockResolvedValue({ data: seasons });

      const [a, b] = await Promise.all([
        store.getSeasons(),
        store.getSeasons(),
      ]);

      expect(mockApi.get).toHaveBeenCalledTimes(1);
      expect(a).toEqual(seasons);
      expect(b).toEqual(seasons);
    });

    it("allows retry after a failed request", async () => {
      mockApi.get.mockRejectedValueOnce(new Error("fail"));
      mockApi.get.mockResolvedValueOnce({ data: seasons });

      await expect(store.getSeasons()).rejects.toThrow("fail");
      const result = await store.getSeasons();

      expect(mockApi.get).toHaveBeenCalledTimes(2);
      expect(result).toEqual(seasons);
    });
  });

  describe("currentSeason", () => {
    it("returns the season marked isCurrent", () => {
      store.seasons = [
        { startYear: 2026, label: "2026/2027", isCurrent: false },
        { startYear: 2025, label: "2025/2026", isCurrent: true },
      ];
      expect(store.currentSeason?.startYear).toBe(2025);
    });

    it("falls back to the first season when none is current", () => {
      store.seasons = [
        { startYear: 2026, label: "2026/2027", isCurrent: false },
        { startYear: 2025, label: "2025/2026", isCurrent: false },
      ];
      expect(store.currentSeason?.startYear).toBe(2026);
    });

    it("returns null when there are no seasons", () => {
      expect(store.currentSeason).toBeNull();
    });
  });
});
