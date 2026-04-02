import { defineStore } from "pinia";
import { api } from "boot/axios";

export const useCompetencyStore = defineStore("competencies", {
  state: () => ({
    competencies: [],
    userCompetencies: {},
  }),
  actions: {
    async getCompetencies() {
      const response = await api.get("/api/competencies");
      this.competencies = response.data;
    },
    async getCompetencyById(id) {
      const response = await api.get(`/api/competencies/${id}`);
      return response.data;
    },
    async createCompetency(request) {
      const response = await api.post("/api/competencies", request);
      this.competencies.push(response.data);
      return response.data;
    },
    async updateCompetency(id, request) {
      const response = await api.put(`/api/competencies/${id}`, request);
      var updatedCompetency = response.data;
      var existing = this.competencies.find((c) => c.id === updatedCompetency.id);
      if (existing) Object.assign(existing, updatedCompetency);
      return response.data;
    },
    async deleteCompetency(id) {
      await api.delete(`/api/competencies/${id}`);
      this.competencies = this.competencies.filter((c) => c.id !== id);
    },
    async getUserCompetencies(userId) {
      const response = await api.get(`/api/competencies/user/${userId}`);
      this.userCompetencies[userId] = response.data;
      return response.data;
    },
    async addUserCompetency(request) {
      const response = await api.post("/api/competencies/user", request);
      return response.data;
    },
    async approveUserCompetency(userCompetencyId, request) {
      const response = await api.put(
        `/api/competencies/user/${userCompetencyId}/approve`,
        request
      );
      return response.data;
    },
    async revokeUserCompetency(userCompetencyId) {
      const response = await api.delete(
        `/api/competencies/user/${userCompetencyId}`
      );
      return response.data;
    },
    async addApprover(competencyId, userId) {
      const response = await api.post(
        `/api/competencies/${competencyId}/approvers/${userId}`
      );
      return response.data;
    },
    async removeApprover(approverId) {
      await api.delete(`/api/competencies/approvers/${approverId}`);
    },
    async getResourceTypeCompetencies(resourceTypeId) {
      const response = await api.get(`/api/resourcetypes/${resourceTypeId}/competencies`);
      return response.data;
    },
    async setResourceTypeCompetencies(resourceTypeId, requirements) {
      // requirements: [{ competencyId, minimumRequired }]
      const response = await api.put(`/api/resourcetypes/${resourceTypeId}/competencies`, requirements);
      return response.data;
    },
  },
});
