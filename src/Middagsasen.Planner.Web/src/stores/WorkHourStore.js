import { defineStore } from "pinia";
import { api } from "boot/axios";

export const useWorkHourStore = defineStore("workHours", {
  state: () => ({
    workHour: {},
    userWorkHours: [],
    workHourById: {},
    activeWorkHour: {},
  }),
  actions: {
    async createWorkHour(model) {
      const payload = {
        startTime: model.startTime,
        endTime: model.endTime,
        description: model.description,
      };
      const response = await api.post(`/api/WorkHours`, payload);
      this.workHour = response.data;
      return response.data;
    },

    async getWorkHours(params) {
      const response = await api.get(`/api/WorkHours/`, {
        params,
      });
      this.userWorkHours = response.data;
      return response.data;
    },

    // season = sesongens startår. Utelatt userId/season = ingen filtrering.
    async getWorkHoursSums(userId = null, season = null) {
      const params = {};
      if (userId !== null && userId !== undefined) params.userId = userId;
      if (season !== null && season !== undefined) params.season = season;
      const response = await api.get(`/api/WorkHours/Sum`, { params });
      return response.data;
    },

    async getWorkHoursByUser(userId, params) {
      const response = await api.get(`/api/WorkHours/User/${userId}`, {
        params,
      });
      this.userWorkHours = response.data;
      return response.data;
    },

    async getWorkHourById(workHourId) {
      const response = await api.get(`/api/WorkHours/${workHourId}`);
      this.workHourById = response.data;
    },

    // Sender kun feltene i `changes` (startTime, endTime, description, approvalStatus).
    async patchWorkHour(workHourId, changes) {
      const response = await api.patch(`/api/WorkHours/${workHourId}`, changes);
      return response.data;
    },

    // approvalStatus: 1 = godkjent, 2 = avslått, null = ingen status.
    async updateApproval(model) {
      const response = await api.patch(
        `/api/WorkHours/${model.workHourId}/ApprovedBy`,
        { approvalStatus: model.approvalStatus }
      );
      return response.data;
    },

    async deleteWorkHourById(workHourId) {
      await api.delete(`/api/WorkHours/${workHourId}`);
    },
  },
});
