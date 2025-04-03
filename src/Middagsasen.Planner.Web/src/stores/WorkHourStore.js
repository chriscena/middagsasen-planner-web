import { defineStore } from "pinia";
import { parseISO, formatISO, addDays } from "date-fns";
import { api } from "boot/axios";
import { useUserStore } from "src/stores/UserStore";


export const useWorkHourStore = defineStore("workHours", {
  state: () => ({
    userWorkHours: [],
    workHourById: {},
  }),
  actions: {
    async createWorkHour(model) {
      const response = await api.post(`/api/WorkHours`, model);
      this.workHours.push(response.data);
    },
    async getWorkHoursByUser(userId) {
      const response = await api.get(`/api/WorkHours/user/${userId}`);
      console.log('lolololl', this.workHours);
      this.userWorkHours = response.data;
    }
  }
})