import { defineStore } from "pinia";
import { api } from "boot/axios";
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

interface CompetencyState {
  competencies: CompetencyResponse[];
  userCompetencies: Record<number, UserCompetencyResponse[]>;
}

export const useCompetencyStore = defineStore("competencies", {
  state: (): CompetencyState => ({
    competencies: [],
    userCompetencies: {},
  }),
  actions: {
    async getCompetencies(): Promise<void> {
      const response = await api.get<CompetencyResponse[]>("/api/competencies");
      this.competencies = response.data;
    },
    async getCompetencyById(id: number): Promise<CompetencyResponse> {
      const response = await api.get<CompetencyResponse>(
        `/api/competencies/${id}`
      );
      return response.data;
    },
    async createCompetency(
      request: CompetencyRequest
    ): Promise<CompetencyResponse> {
      const response = await api.post<CompetencyResponse>(
        "/api/competencies",
        request
      );
      this.competencies.push(response.data);
      return response.data;
    },
    async updateCompetency(
      id: number,
      request: CompetencyRequest
    ): Promise<CompetencyResponse> {
      const response = await api.put<CompetencyResponse>(
        `/api/competencies/${id}`,
        request
      );
      const updatedCompetency = response.data;
      const existing = this.competencies.find(
        (c) => c.id === updatedCompetency.id
      );
      if (existing) Object.assign(existing, updatedCompetency);
      return response.data;
    },
    async deleteCompetency(id: number): Promise<void> {
      await api.delete(`/api/competencies/${id}`);
      this.competencies = this.competencies.filter((c) => c.id !== id);
    },
    async getUserCompetencies(
      userId: number
    ): Promise<UserCompetencyResponse[]> {
      const response = await api.get<UserCompetencyResponse[]>(
        `/api/competencies/user/${userId}`
      );
      this.userCompetencies[userId] = response.data;
      return response.data;
    },
    async addUserCompetency(
      request: UserCompetencyRequest
    ): Promise<UserCompetencyResponse> {
      const response = await api.post<UserCompetencyResponse>(
        "/api/competencies/user",
        request
      );
      return response.data;
    },
    async approveUserCompetency(
      userCompetencyId: number,
      request: ApproveCompetencyRequest
    ): Promise<UserCompetencyResponse> {
      const response = await api.put<UserCompetencyResponse>(
        `/api/competencies/user/${userCompetencyId}/approve`,
        request
      );
      return response.data;
    },
    async revokeUserCompetency(
      userCompetencyId: number
    ): Promise<UserCompetencyResponse> {
      const response = await api.delete<UserCompetencyResponse>(
        `/api/competencies/user/${userCompetencyId}`
      );
      return response.data;
    },
    async addApprover(
      competencyId: number,
      userId: number
    ): Promise<CompetencyApproverResponse> {
      const response = await api.post<CompetencyApproverResponse>(
        `/api/competencies/${competencyId}/approvers/${userId}`
      );
      return response.data;
    },
    async removeApprover(approverId: number): Promise<void> {
      await api.delete(`/api/competencies/approvers/${approverId}`);
    },
    async getResourceTypeCompetencies(
      resourceTypeId: number
    ): Promise<ResourceTypeCompetencyResponse[]> {
      const response = await api.get<ResourceTypeCompetencyResponse[]>(
        `/api/resourcetypes/${resourceTypeId}/competencies`
      );
      return response.data;
    },
    async setResourceTypeCompetencies(
      resourceTypeId: number,
      requirements: SetResourceTypeCompetencyRequest[]
    ): Promise<ResourceTypeCompetencyResponse[]> {
      // requirements: [{ competencyId, minimumRequired }]
      const response = await api.put<ResourceTypeCompetencyResponse[]>(
        `/api/resourcetypes/${resourceTypeId}/competencies`,
        requirements
      );
      return response.data;
    },
  },
});
