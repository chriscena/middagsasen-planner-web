import { defineStore } from "pinia";
import { parseISO, formatISO, addDays } from "date-fns";
import { api } from "boot/axios";
import { useUserStore } from "src/stores/UserStore";

export const useWorkHourStore = defineStore("workHours", {
  state: () => ({
    workHour: {},
    userWorkHours: [],
    workHourById: {},
    activeWorkHour: {},
  }),
  actions: {
    async createWorkHour(model) {
      const response = await api.post(`/api/WorkHours`, model);
      this.workHour = response.data;
    },

    async getActiveWorkHour(userId) {
      const response = await api.get(`/api/WorkHours/User/${userId}/EndTime`);
      this.activeWorkHour = response.data; 
    },

    async getWorkHoursByUser(userId, params) {
      const response = await api.get(`/api/WorkHours/User/${userId}`, {
        params,
      });
      this.userWorkHours = response.data;
    },

    async getWorkHourById(workHourId) {
      const response = await api.get(`/api/WorkHours/${workHourId}`);
      this.workHourById = response.data;
    },
    async updateWorkHourEndTime(model) {
      const response = await api.patch(
        `/api/WorkHours/${model.workHourId}/EndTime`,
        model
      );
    },
    async updateWorkHour(model) {
      const response = await api.put(
        `/api/WorkHours/${model.workHourId}`,
        model
      );
    },
    async updateApprovedBy(model) {
      const response = await api.patch(
        `/api/WorkHours/${model.workHourId}/ApprovedBy`,
        model
      );
    },
    async deleteWorkHourById(workHourId) {
        await api.delete(
        `/api/WorkHours/${workHourId}`,
      );
    },
  },
});
