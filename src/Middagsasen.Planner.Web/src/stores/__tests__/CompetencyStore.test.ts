import { vi, describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type {
  ApproveCompetencyRequest,
  CompetencyApproverResponse,
  CompetencyRequest,
  CompetencyResponse,
  ResourceTypeCompetencyResponse,
  SetResourceTypeCompetencyRequest,
  UserCompetencyRequest,
  UserCompetencyResponse,
} from "src/types";

// Mock the axios api - use vi.hoisted so the variable is available in the hoisted vi.mock factory
const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}));

vi.mock("boot/axios", () => ({
  api: mockApi,
}));

import { useCompetencyStore } from "stores/CompetencyStore";

function competency(
  fields: Pick<CompetencyResponse, "id" | "name"> & Partial<CompetencyResponse>
): CompetencyResponse {
  return {
    hasExpiry: false,
    inactive: false,
    resourceTypes: [],
    approvers: [],
    ...fields,
  };
}

function competencyRequest(
  fields: Pick<CompetencyRequest, "name"> & Partial<CompetencyRequest>
): CompetencyRequest {
  return { hasExpiry: false, ...fields };
}

function userCompetency(
  fields: Pick<UserCompetencyResponse, "id"> & Partial<UserCompetencyResponse>
): UserCompetencyResponse {
  return {
    userId: 42,
    userFullName: "Ola Nordmann",
    competencyId: 3,
    competencyName: "First Aid",
    approved: false,
    isExpired: false,
    created: "2026-01-01T00:00:00",
    ...fields,
  };
}

describe("CompetencyStore", () => {
  let store: ReturnType<typeof useCompetencyStore>;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useCompetencyStore();
    vi.clearAllMocks();
  });

  describe("getCompetencies", () => {
    it("should fetch competencies and populate state", async () => {
      const competencies = [
        competency({ id: 1, name: "First Aid" }),
        competency({ id: 2, name: "CPR" }),
      ];
      mockApi.get.mockResolvedValue({ data: competencies });

      await store.getCompetencies();

      expect(mockApi.get).toHaveBeenCalledWith("/api/competencies");
      expect(store.competencies).toEqual(competencies);
    });

    it("should replace existing state on re-fetch", async () => {
      store.competencies = [competency({ id: 99, name: "Old" })];

      const newCompetencies = [competency({ id: 1, name: "New" })];
      mockApi.get.mockResolvedValue({ data: newCompetencies });

      await store.getCompetencies();

      expect(store.competencies).toEqual(newCompetencies);
      expect(store.competencies).toHaveLength(1);
    });
  });

  describe("getCompetencyById", () => {
    it("should return competency data", async () => {
      const lifeguard = competency({ id: 5, name: "Lifeguard" });
      mockApi.get.mockResolvedValue({ data: lifeguard });

      const result = await store.getCompetencyById(5);

      expect(mockApi.get).toHaveBeenCalledWith("/api/competencies/5");
      expect(result).toEqual(lifeguard);
    });
  });

  describe("createCompetency", () => {
    it("should POST and add to state array", async () => {
      const request = competencyRequest({ name: "New Cert" });
      const created = competency({ id: 10, name: "New Cert" });
      mockApi.post.mockResolvedValue({ data: created });

      await store.createCompetency(request);

      expect(mockApi.post).toHaveBeenCalledWith("/api/competencies", request);
      expect(store.competencies).toContainEqual(created);
    });

    it("should return created competency", async () => {
      const created = competency({ id: 10, name: "New Cert" });
      mockApi.post.mockResolvedValue({ data: created });

      const result = await store.createCompetency(
        competencyRequest({ name: "New Cert" })
      );

      expect(result).toEqual(created);
    });
  });

  describe("updateCompetency", () => {
    it("should PUT and update existing item in state array", async () => {
      store.competencies = [
        competency({ id: 1, name: "Old Name", description: "Old" }),
        competency({ id: 2, name: "Other" }),
      ];

      const updated = competency({
        id: 1,
        name: "Updated Name",
        description: "New",
      });
      const request = competencyRequest({
        name: "Updated Name",
        description: "New",
      });
      mockApi.put.mockResolvedValue({ data: updated });

      const result = await store.updateCompetency(1, request);

      expect(mockApi.put).toHaveBeenCalledWith("/api/competencies/1", request);
      expect(store.competencies.find((c) => c.id === 1)).toEqual(updated);
      expect(store.competencies).toHaveLength(2);
      expect(result).toEqual(updated);
    });
  });

  describe("deleteCompetency", () => {
    it("should DELETE and remove from state array", async () => {
      store.competencies = [
        competency({ id: 1, name: "Keep" }),
        competency({ id: 2, name: "Remove" }),
      ];
      mockApi.delete.mockResolvedValue({});

      await store.deleteCompetency(2);

      expect(mockApi.delete).toHaveBeenCalledWith("/api/competencies/2");
      expect(store.competencies).toEqual([competency({ id: 1, name: "Keep" })]);
    });
  });

  describe("getUserCompetencies", () => {
    it("should fetch and store by userId key", async () => {
      const userComps = [
        userCompetency({ id: 1, competencyId: 3, userId: 42 }),
      ];
      mockApi.get.mockResolvedValue({ data: userComps });

      await store.getUserCompetencies(42);

      expect(mockApi.get).toHaveBeenCalledWith("/api/competencies/user/42");
      expect(store.userCompetencies[42]).toEqual(userComps);
    });

    it("should return the data", async () => {
      const userComps = [
        userCompetency({ id: 1, competencyId: 3, userId: 42 }),
      ];
      mockApi.get.mockResolvedValue({ data: userComps });

      const result = await store.getUserCompetencies(42);

      expect(result).toEqual(userComps);
    });
  });

  describe("addUserCompetency", () => {
    it("should POST and return data", async () => {
      const request: UserCompetencyRequest = { userId: 42, competencyId: 3 };
      const created = userCompetency({ id: 7, userId: 42, competencyId: 3 });
      mockApi.post.mockResolvedValue({ data: created });

      const result = await store.addUserCompetency(request);

      expect(mockApi.post).toHaveBeenCalledWith(
        "/api/competencies/user",
        request
      );
      expect(result).toEqual(created);
    });
  });

  describe("approveUserCompetency", () => {
    it("should PUT and return data", async () => {
      const request: ApproveCompetencyRequest = {
        expiryDate: "2027-06-01T00:00:00",
      };
      const approved = userCompetency({ id: 7, approved: true });
      mockApi.put.mockResolvedValue({ data: approved });

      const result = await store.approveUserCompetency(7, request);

      expect(mockApi.put).toHaveBeenCalledWith(
        "/api/competencies/user/7/approve",
        request
      );
      expect(result).toEqual(approved);
    });
  });

  describe("revokeUserCompetency", () => {
    it("should DELETE and return data", async () => {
      const revoked = userCompetency({ id: 7 });
      mockApi.delete.mockResolvedValue({ data: revoked });

      const result = await store.revokeUserCompetency(7);

      expect(mockApi.delete).toHaveBeenCalledWith("/api/competencies/user/7");
      expect(result).toEqual(revoked);
    });
  });

  describe("addApprover", () => {
    it("should POST and return data", async () => {
      const approver: CompetencyApproverResponse = {
        id: 15,
        userId: 42,
        fullName: "Ola Nordmann",
      };
      mockApi.post.mockResolvedValue({ data: approver });

      const result = await store.addApprover(3, 42);

      expect(mockApi.post).toHaveBeenCalledWith(
        "/api/competencies/3/approvers/42"
      );
      expect(result).toEqual(approver);
    });
  });

  describe("removeApprover", () => {
    it("should DELETE", async () => {
      mockApi.delete.mockResolvedValue({});

      await store.removeApprover(15);

      expect(mockApi.delete).toHaveBeenCalledWith(
        "/api/competencies/approvers/15"
      );
    });
  });

  describe("getResourceTypeCompetencies", () => {
    it("should GET and return data", async () => {
      const requirements: ResourceTypeCompetencyResponse[] = [
        { competencyId: 1, competencyName: "First Aid", minimumRequired: 2 },
        { competencyId: 2, competencyName: "CPR", minimumRequired: 1 },
      ];
      mockApi.get.mockResolvedValue({ data: requirements });

      const result = await store.getResourceTypeCompetencies(10);

      expect(mockApi.get).toHaveBeenCalledWith(
        "/api/resourcetypes/10/competencies"
      );
      expect(result).toEqual(requirements);
    });
  });

  describe("setResourceTypeCompetencies", () => {
    it("should PUT and return data", async () => {
      const requirements: SetResourceTypeCompetencyRequest[] = [
        { competencyId: 1, minimumRequired: 2 },
        { competencyId: 3, minimumRequired: 1 },
      ];
      const responseData: ResourceTypeCompetencyResponse[] = [
        { competencyId: 1, competencyName: "First Aid", minimumRequired: 2 },
        { competencyId: 3, competencyName: "Driving", minimumRequired: 1 },
      ];
      mockApi.put.mockResolvedValue({ data: responseData });

      const result = await store.setResourceTypeCompetencies(10, requirements);

      expect(mockApi.put).toHaveBeenCalledWith(
        "/api/resourcetypes/10/competencies",
        requirements
      );
      expect(result).toEqual(responseData);
    });
  });
});
