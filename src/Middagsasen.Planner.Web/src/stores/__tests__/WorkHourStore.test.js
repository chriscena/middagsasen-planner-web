import { vi, describe, it, expect, beforeEach } from 'vitest';
import { setActivePinia, createPinia } from 'pinia';

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));

vi.mock('boot/axios', () => ({
  api: mockApi,
}));

import { useWorkHourStore } from 'stores/WorkHourStore';

describe('WorkHourStore', () => {
  let store;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useWorkHourStore();
    vi.clearAllMocks();
  });

  describe('createWorkHour', () => {
    it('sends only startTime, endTime and description and returns data', async () => {
      const created = { workHourId: 1 };
      mockApi.post.mockResolvedValue({ data: created });

      const result = await store.createWorkHour({
        startTime: 's',
        endTime: 'e',
        description: 'd',
        userId: 42,
      });

      expect(mockApi.post).toHaveBeenCalledWith('/api/WorkHours', {
        startTime: 's',
        endTime: 'e',
        description: 'd',
      });
      expect(result).toEqual(created);
    });
  });

  describe('patchWorkHour', () => {
    it('sends changes as-is to PATCH /api/WorkHours/{id}', async () => {
      const updated = { workHourId: 5, approvalStatus: 1 };
      mockApi.patch.mockResolvedValue({ data: updated });

      const result = await store.patchWorkHour(5, { approvalStatus: 1 });

      expect(mockApi.patch).toHaveBeenCalledWith('/api/WorkHours/5', {
        approvalStatus: 1,
      });
      expect(result).toEqual(updated);
    });
  });

  describe('updateApproval', () => {
    it('sends only approvalStatus', async () => {
      mockApi.patch.mockResolvedValue({ data: {} });

      await store.updateApproval({
        workHourId: 7,
        approvalStatus: null,
        approvedBy: 3,
      });

      expect(mockApi.patch).toHaveBeenCalledWith(
        '/api/WorkHours/7/ApprovedBy',
        { approvalStatus: null }
      );
    });
  });
});
