import { apiClient } from '../../lib/apiClient';
import type { Department } from '../catalog/types';

interface Branch {
  id: string;
  name: string;
}

// Departments are owned by the onboarding feature, not catalog. Item Catalog only needs a
// read-only flattened list for the department-assignment picker, mirroring
// `allDepartmentsProvider` in the Flutter client (list branches, then list departments per branch).
export const departmentsApi = {
  listAllDepartments: async (): Promise<Department[]> => {
    const branches = (await apiClient.get<Branch[]>('/branches')).data;
    const perBranch = await Promise.all(
      branches.map((branch) =>
        apiClient
          .get<Omit<Department, 'branchId'>[]>(`/branches/${branch.id}/departments`)
          .then((r) => r.data.map((d) => ({ ...d, branchId: branch.id }))),
      ),
    );
    return perBranch.flat();
  },
};
