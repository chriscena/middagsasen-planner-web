import { defineStore } from "pinia";
import { api } from "@/boot/axios";
import { useAuthStore } from "@/stores/AuthStore";
import type {
  PhoneResponse,
  UpdateMeRequest,
  UserRequest,
  UserResponse,
  UserWorkHourSumResponse,
} from "@/types";

const authStore = useAuthStore();

interface UserState {
  users: UserResponse[];
  phoneList: PhoneResponse[];
  workHourSums: UserWorkHourSumResponse[];
}

// UsersPage sender hele det redigerte brukerobjektet (inkl. id og øvrige
// felt fra UserResponse); id brukes kun i URL-en.
type UserUpdate = UserRequest & { id: number };

export const useUserStore = defineStore("users", {
  state: (): UserState => ({
    users: [],
    phoneList: [],
    workHourSums: [],
  }),
  // getters: {
  //   doubleCount: (state) => state.counter * 2,
  // },
  actions: {
    async getUser(): Promise<void> {
      try {
        const userResponse = await api.get<UserResponse>("/api/me");
        authStore.setUser(userResponse.data);
      } catch {
        // 401 håndteres av response-interceptoren i boot/axios.js. Øvrige feil
        // svelges fordi getUser() ofte kalles uten await/catch.
      }
    },
    async getUsers(): Promise<void> {
      const response = await api.get<UserResponse[]>("/api/users");
      this.users.splice(0, this.users.length);
      this.users.push(...response.data);
    },
    async createUser(user: UserRequest): Promise<void> {
      const response = await api.post<UserResponse>("/api/users", user);
      this.users.push(response.data);
    },
    async updateUser(user: UserUpdate): Promise<void> {
      const response = await api.put<UserResponse>(
        `/api/users/${user.id}`,
        user
      );
      const updatedUser = response.data;
      const existingUser = this.users.find((u) => u.id === updatedUser.id);
      if (existingUser) Object.assign(existingUser, updatedUser);
    },
    async deleteUser(user: Pick<UserResponse, "id">): Promise<void> {
      // JS-versjonen sendte `user` som andre argument, som for delete er
      // axios-config (ikke body). Ingen av feltene er config-nøkler, så det
      // hadde ingen effekt og er fjernet.
      const response = await api.delete<UserResponse>(`/api/users/${user.id}`);
      const deletedUser = response.data;
      this.users = this.users.filter((u) => u.id !== deletedUser.id);
    },
    async saveUser(user: UpdateMeRequest): Promise<void> {
      const response = await api.put<UserResponse>("/api/me", user);
      authStore.setUser(response.data);
    },
    async getWorkHourSums(): Promise<void> {
      const response = await api.get<UserWorkHourSumResponse[]>(
        "/api/WorkHours/Sum/All"
      );
      this.workHourSums = response.data;
    },
    async getPhoneList(): Promise<void> {
      const response = await api.get<PhoneResponse[]>("/api/users/phone");
      this.phoneList = response.data;
    },
    async logout(): Promise<void> {
      try {
        await api.post("/api/authentication/logout");
      } catch {
        // Utlogging lokalt skal skje selv om kallet feiler.
      }
      authStore.removeUserSession();
    },
  },
});
