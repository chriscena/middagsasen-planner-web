import { vi, describe, it, expect, beforeEach } from 'vitest';
import { setActivePinia, createPinia } from 'pinia';

// Mock the axios api - use vi.hoisted so the variable is available in the hoisted vi.mock factory
const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}));

vi.mock('boot/axios', () => ({
  api: mockApi,
}));

import { useCompetencyStore } from 'stores/CompetencyStore';

describe('CompetencyStore', () => {
  let store;

  beforeEach(() => {
    setActivePinia(createPinia());
    store = useCompetencyStore();
    vi.clearAllMocks();
  });

  describe('getCompetencies', () => {
    it('should fetch competencies and populate state', async () => {
      const competencies = [
        { id: 1, name: 'First Aid' },
        { id: 2, name: 'CPR' },
      ];
      mockApi.get.mockResolvedValue({ data: competencies });

      await store.getCompetencies();

      expect(mockApi.get).toHaveBeenCalledWith('/api/competencies');
      expect(store.competencies).toEqual(competencies);
    });

    it('should replace existing state on re-fetch', async () => {
      store.competencies = [{ id: 99, name: 'Old' }];

      const newCompetencies = [{ id: 1, name: 'New' }];
      mockApi.get.mockResolvedValue({ data: newCompetencies });

      await store.getCompetencies();

      expect(store.competencies).toEqual(newCompetencies);
      expect(store.competencies).toHaveLength(1);
    });
  });

  describe('getCompetencyById', () => {
    it('should return competency data', async () => {
      const competency = { id: 5, name: 'Lifeguard' };
      mockApi.get.mockResolvedValue({ data: competency });

      const result = await store.getCompetencyById(5);

      expect(mockApi.get).toHaveBeenCalledWith('/api/competencies/5');
      expect(result).toEqual(competency);
    });
  });

  describe('createCompetency', () => {
    it('should POST and add to state array', async () => {
      const request = { name: 'New Cert' };
      const created = { id: 10, name: 'New Cert' };
      mockApi.post.mockResolvedValue({ data: created });

      await store.createCompetency(request);

      expect(mockApi.post).toHaveBeenCalledWith('/api/competencies', request);
      expect(store.competencies).toContainEqual(created);
    });

    it('should return created competency', async () => {
      const created = { id: 10, name: 'New Cert' };
      mockApi.post.mockResolvedValue({ data: created });

      const result = await store.createCompetency({ name: 'New Cert' });

      expect(result).toEqual(created);
    });
  });

  describe('updateCompetency', () => {
    it('should PUT and update existing item in state array', async () => {
      store.competencies = [
        { id: 1, name: 'Old Name', description: 'Old' },
        { id: 2, name: 'Other' },
      ];

      const updated = { id: 1, name: 'Updated Name', description: 'New' };
      const request = { name: 'Updated Name', description: 'New' };
      mockApi.put.mockResolvedValue({ data: updated });

      const result = await store.updateCompetency(1, request);

      expect(mockApi.put).toHaveBeenCalledWith('/api/competencies/1', request);
      expect(store.competencies.find((c) => c.id === 1)).toEqual(updated);
      expect(store.competencies).toHaveLength(2);
      expect(result).toEqual(updated);
    });
  });

  describe('deleteCompetency', () => {
    it('should DELETE and remove from state array', async () => {
      store.competencies = [
        { id: 1, name: 'Keep' },
        { id: 2, name: 'Remove' },
      ];
      mockApi.delete.mockResolvedValue({});

      await store.deleteCompetency(2);

      expect(mockApi.delete).toHaveBeenCalledWith('/api/competencies/2');
      expect(store.competencies).toEqual([{ id: 1, name: 'Keep' }]);
    });
  });

  describe('getUserCompetencies', () => {
    it('should fetch and store by userId key', async () => {
      const userComps = [{ id: 1, competencyId: 3, userId: 42 }];
      mockApi.get.mockResolvedValue({ data: userComps });

      await store.getUserCompetencies(42);

      expect(mockApi.get).toHaveBeenCalledWith('/api/competencies/user/42');
      expect(store.userCompetencies[42]).toEqual(userComps);
    });

    it('should return the data', async () => {
      const userComps = [{ id: 1, competencyId: 3, userId: 42 }];
      mockApi.get.mockResolvedValue({ data: userComps });

      const result = await store.getUserCompetencies(42);

      expect(result).toEqual(userComps);
    });
  });

  describe('addUserCompetency', () => {
    it('should POST and return data', async () => {
      const request = { userId: 42, competencyId: 3 };
      const created = { id: 7, userId: 42, competencyId: 3 };
      mockApi.post.mockResolvedValue({ data: created });

      const result = await store.addUserCompetency(request);

      expect(mockApi.post).toHaveBeenCalledWith('/api/competencies/user', request);
      expect(result).toEqual(created);
    });
  });

  describe('approveUserCompetency', () => {
    it('should PUT and return data', async () => {
      const request = { approvedBy: 'Admin' };
      const approved = { id: 7, approved: true };
      mockApi.put.mockResolvedValue({ data: approved });

      const result = await store.approveUserCompetency(7, request);

      expect(mockApi.put).toHaveBeenCalledWith(
        '/api/competencies/user/7/approve',
        request
      );
      expect(result).toEqual(approved);
    });
  });

  describe('revokeUserCompetency', () => {
    it('should DELETE and return data', async () => {
      const revoked = { id: 7, revoked: true };
      mockApi.delete.mockResolvedValue({ data: revoked });

      const result = await store.revokeUserCompetency(7);

      expect(mockApi.delete).toHaveBeenCalledWith('/api/competencies/user/7');
      expect(result).toEqual(revoked);
    });
  });

  describe('addApprover', () => {
    it('should POST and return data', async () => {
      const approver = { id: 15, competencyId: 3, userId: 42 };
      mockApi.post.mockResolvedValue({ data: approver });

      const result = await store.addApprover(3, 42);

      expect(mockApi.post).toHaveBeenCalledWith(
        '/api/competencies/3/approvers/42'
      );
      expect(result).toEqual(approver);
    });
  });

  describe('removeApprover', () => {
    it('should DELETE', async () => {
      mockApi.delete.mockResolvedValue({});

      await store.removeApprover(15);

      expect(mockApi.delete).toHaveBeenCalledWith(
        '/api/competencies/approvers/15'
      );
    });
  });

  describe('getResourceTypeCompetencies', () => {
    it('should GET and return data', async () => {
      const requirements = [
        { competencyId: 1, competencyName: 'First Aid', minimumRequired: 2 },
        { competencyId: 2, competencyName: 'CPR', minimumRequired: 1 },
      ];
      mockApi.get.mockResolvedValue({ data: requirements });

      const result = await store.getResourceTypeCompetencies(10);

      expect(mockApi.get).toHaveBeenCalledWith('/api/resourcetypes/10/competencies');
      expect(result).toEqual(requirements);
    });
  });

  describe('setResourceTypeCompetencies', () => {
    it('should PUT and return data', async () => {
      const requirements = [
        { competencyId: 1, minimumRequired: 2 },
        { competencyId: 3, minimumRequired: 1 },
      ];
      const responseData = [
        { competencyId: 1, competencyName: 'First Aid', minimumRequired: 2 },
        { competencyId: 3, competencyName: 'Driving', minimumRequired: 1 },
      ];
      mockApi.put.mockResolvedValue({ data: responseData });

      const result = await store.setResourceTypeCompetencies(10, requirements);

      expect(mockApi.put).toHaveBeenCalledWith(
        '/api/resourcetypes/10/competencies',
        requirements
      );
      expect(result).toEqual(responseData);
    });
  });
});
