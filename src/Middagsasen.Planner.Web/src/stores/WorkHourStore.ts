import { defineStore } from "pinia";
import { api } from "boot/axios";
import type {
  ApprovedByResponse,
  CreateWorkHourRequest,
  PagedResponseOfWorkHourResponse,
  UpdateWorkHourRequest,
  WorkHourResponse,
  WorkHourSumResponse,
} from "src/types";

// Initialverdiene i state er tomme objekter/lister (bevart fra JS-versjonen),
// selv om feltene senere fylles med DTO-er.
type EmptyObject = Record<string, never>;

interface WorkHourState {
  workHour: WorkHourResponse | EmptyObject;
  // Starter som [] men erstattes med et PagedResponse fra API-et.
  userWorkHours: PagedResponseOfWorkHourResponse | never[];
  workHourById: WorkHourResponse | EmptyObject;
  // Brukes ikke i dag.
  activeWorkHour: EmptyObject;
}

// Query-parametre til GET /api/WorkHours og /api/WorkHours/User/{userId}.
// null-verdier utelates av axios fra URL-en.
interface WorkHourQuery {
  page?: number | null;
  pageSize?: number | null;
  approved?: number | null;
  season?: number | null;
  userId?: number | null;
}

export const useWorkHourStore = defineStore("workHours", {
  state: (): WorkHourState => ({
    workHour: {},
    userWorkHours: [],
    workHourById: {},
    activeWorkHour: {},
  }),
  actions: {
    async createWorkHour(
      model: CreateWorkHourRequest
    ): Promise<WorkHourResponse> {
      const payload = {
        startTime: model.startTime,
        endTime: model.endTime,
        description: model.description,
      };
      const response = await api.post<WorkHourResponse>(
        `/api/WorkHours`,
        payload
      );
      this.workHour = response.data;
      return response.data;
    },

    async getWorkHours(
      params: WorkHourQuery
    ): Promise<PagedResponseOfWorkHourResponse> {
      const response = await api.get<PagedResponseOfWorkHourResponse>(
        `/api/WorkHours/`,
        {
          params,
        }
      );
      this.userWorkHours = response.data;
      return response.data;
    },

    // season = sesongens startår. Utelatt userId/season = ingen filtrering
    // (axios utelater null/undefined params fra URL-en).
    async getWorkHoursSums(
      userId: number | null = null,
      season: number | null = null
    ): Promise<WorkHourSumResponse> {
      const response = await api.get<WorkHourSumResponse>(
        `/api/WorkHours/Sum`,
        {
          params: { userId, season },
        }
      );
      return response.data;
    },

    async getWorkHoursByUser(
      userId: number,
      params: Omit<WorkHourQuery, "userId">
    ): Promise<PagedResponseOfWorkHourResponse> {
      const response = await api.get<PagedResponseOfWorkHourResponse>(
        `/api/WorkHours/User/${userId}`,
        {
          params,
        }
      );
      this.userWorkHours = response.data;
      return response.data;
    },

    async getWorkHourById(workHourId: number): Promise<void> {
      const response = await api.get<WorkHourResponse>(
        `/api/WorkHours/${workHourId}`
      );
      this.workHourById = response.data;
    },

    // Sender kun feltene i `changes` (startTime, endTime, description, approvalStatus).
    async patchWorkHour(
      workHourId: number,
      changes: UpdateWorkHourRequest
    ): Promise<WorkHourResponse> {
      const response = await api.patch<WorkHourResponse>(
        `/api/WorkHours/${workHourId}`,
        changes
      );
      return response.data;
    },

    // approvalStatus: 1 = godkjent, 2 = avslått, null = ingen status.
    async updateApproval(model: {
      workHourId: number;
      approvalStatus: number | null;
    }): Promise<ApprovedByResponse> {
      const response = await api.patch<ApprovedByResponse>(
        `/api/WorkHours/${model.workHourId}/ApprovedBy`,
        { approvalStatus: model.approvalStatus }
      );
      return response.data;
    },

    async deleteWorkHourById(workHourId: number): Promise<void> {
      await api.delete(`/api/WorkHours/${workHourId}`);
    },
  },
});
