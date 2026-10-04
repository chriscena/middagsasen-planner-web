import { vi, describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { ApprovalFilter, ApprovalStatus } from "@/types";
import type {
  PagedResponseOfWorkHourResponse,
  WorkHourResponse,
  WorkHourSumResponse,
} from "@/types";

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));

vi.mock("@/boot/axios", () => ({
  api: mockApi,
}));

import { useWorkHourStore } from "@/stores/WorkHourStore";

describe("WorkHourStore", () => {
  let store: ReturnType<typeof useWorkHourStore>;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useWorkHourStore();
    vi.clearAllMocks();
  });

  describe("createWorkHour", () => {
    it("sends only startTime, endTime and description and returns data", async () => {
      const created: WorkHourResponse = {
        workHourId: 1,
        userId: 42,
        canEdit: true,
        canDelete: true,
        canApprove: false,
        canResetStatus: false,
      };
      mockApi.post.mockResolvedValue({ data: created });

      // Ekstra felt (userId) skal ikke sendes videre; lagt i en variabel så
      // TypeScript tillater feltet utover CreateWorkHourRequest.
      const model = {
        startTime: "s",
        endTime: "e",
        description: "d",
        userId: 42,
      };
      const result = await store.createWorkHour(model);

      expect(mockApi.post).toHaveBeenCalledWith("/api/WorkHours", {
        startTime: "s",
        endTime: "e",
        description: "d",
      });
      expect(result).toEqual(created);
    });
  });

  describe("patchWorkHour", () => {
    it("sends changes as-is to PATCH /api/WorkHours/{id}", async () => {
      const updated: WorkHourResponse = {
        workHourId: 5,
        userId: 42,
        approvalStatus: ApprovalStatus.Approved,
        canEdit: false,
        canDelete: false,
        canApprove: false,
        canResetStatus: true,
      };
      mockApi.patch.mockResolvedValue({ data: updated });

      const result = await store.patchWorkHour(5, {
        approvalStatus: ApprovalStatus.Approved,
      });

      expect(mockApi.patch).toHaveBeenCalledWith("/api/WorkHours/5", {
        approvalStatus: ApprovalStatus.Approved,
      });
      expect(result).toEqual(updated);
    });
  });

  describe("updateApproval", () => {
    it("sends only approvalStatus", async () => {
      mockApi.patch.mockResolvedValue({ data: {} });

      // Ekstra felt (approvedBy) skal ikke sendes videre.
      const model = {
        workHourId: 7,
        approvalStatus: null,
        approvedBy: 3,
      };
      await store.updateApproval(model);

      expect(mockApi.patch).toHaveBeenCalledWith(
        "/api/WorkHours/7/ApprovedBy",
        { approvalStatus: null }
      );
    });
  });

  describe("getWorkHoursSums", () => {
    it("sends userId and season as params", async () => {
      const sums: WorkHourSumResponse = {
        approvedHours: 1,
        pendingHours: 2,
        rejectedHours: 3,
      };
      mockApi.get.mockResolvedValue({ data: sums });

      const result = await store.getWorkHoursSums(4, 2025);

      expect(mockApi.get).toHaveBeenCalledWith("/api/WorkHours/Sum", {
        params: { userId: 4, season: 2025 },
      });
      expect(result).toEqual(sums);
    });

    it("passes null values through (axios drops them from the URL)", async () => {
      mockApi.get.mockResolvedValue({ data: {} });

      await store.getWorkHoursSums(null, 2025);
      expect(mockApi.get).toHaveBeenLastCalledWith("/api/WorkHours/Sum", {
        params: { userId: null, season: 2025 },
      });

      await store.getWorkHoursSums();
      expect(mockApi.get).toHaveBeenLastCalledWith("/api/WorkHours/Sum", {
        params: { userId: null, season: null },
      });
    });
  });

  describe("getWorkHours", () => {
    it("forwards season and userId params", async () => {
      const data: PagedResponseOfWorkHourResponse = {
        result: [],
        totalCount: 0,
      };
      mockApi.get.mockResolvedValue({ data });
      const params = {
        approved: ApprovalFilter.Pending,
        page: 1,
        pageSize: 15,
        season: 2025,
        userId: 7,
      };

      const result = await store.getWorkHours(params);

      expect(mockApi.get).toHaveBeenCalledWith("/api/WorkHours/", { params });
      expect(result).toEqual(data);
    });
  });

  describe("getWorkHoursByUser", () => {
    it("forwards season param", async () => {
      const data: PagedResponseOfWorkHourResponse = {
        result: [],
        totalCount: 0,
      };
      mockApi.get.mockResolvedValue({ data });
      const params = { page: 1, pageSize: 20, season: 2024 };

      await store.getWorkHoursByUser(9, params);

      expect(mockApi.get).toHaveBeenCalledWith("/api/WorkHours/User/9", {
        params,
      });
    });
  });
});
