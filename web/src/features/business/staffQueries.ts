import { useQuery } from '@tanstack/react-query';
import { staffApi } from './staffApi';

export const staffKeys = { all: ['staff', 'people'] as const };

export const useStaff = () => useQuery({ queryKey: staffKeys.all, queryFn: () => staffApi.list() });
