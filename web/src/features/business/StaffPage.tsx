import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../components/feedback/toastStore';
import { EditorCard } from '../../components/forms/EditorCard';
import { FormField, controlClass } from '../../components/forms/FormField';
import { FormLoader } from '../../components/forms/FormLoader';
import { ListCard, Pill, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { useSession } from '../auth/useSession';
import { useBranches } from '../branches/queries';
import type { Branch } from '../branches/types';
import { useDepartments } from '../catalog/queries';
import type { Department } from '../catalog/types';
import type { StaffMember } from './staffApi';
import { useCreateStaff, useStaff, useUpdateStaff } from './staffQueries';
import { pinProblem, scopeChoicesFor, scopeFields, staffSchema, type StaffForm } from './staffRules';
import { Role, ScopeType, STAFF_ROLES, labelOf, roleLabels, scopeLabels } from './types';

function describeScope(member: StaffMember, branches: Branch[], departments: Department[]): string {
  if (member.scopeType === ScopeType.Branch) return branches.find((b) => b.id === member.scopeId)?.name ?? 'One branch';
  if (member.scopeType === ScopeType.Department) return departments.find((d) => d.id === member.scopeId)?.name ?? 'One department';
  return 'Whole business';
}

export function StaffPage() {
  const { role } = useSession();
  const isAdmin = role === 'Admin';
  const staff = useStaff();
  const branches = useBranches();
  const departments = useDepartments();
  const [editing, setEditing] = useState<StaffMember | null>(null);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Staff" subtitle={isAdmin ? undefined : 'Only an admin can add or change staff.'} backTo={{ to: '/business', label: 'Business' }} />
      <div className={isAdmin ? 'grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]' : ''}>
        <QueryList
          query={staff}
          errorTitle="Staff could not be loaded"
          emptyMessage="No staff yet."
          renderRow={(member) => (
            <ListCard key={member.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">{member.name}</p>
                <p className="text-base">
                  {labelOf(roleLabels, member.role)}, {describeScope(member, branches.data ?? [], departments.data ?? [])}
                </p>
                {isAdmin && (
                  <button type="button" onClick={() => setEditing(member)} className="mt-2 h-12 text-base font-semibold text-brand-strong underline">
                    Edit
                  </button>
                )}
              </div>
              {!member.isActive && <Pill>Inactive</Pill>}
            </ListCard>
          )}
        />
        {isAdmin && (
          <FormLoader
            failed={branches.isError ? branches : departments.isError ? departments : null}
            ready={!!(branches.data && departments.data)}
          >
            {branches.data && departments.data && (
              <StaffEditor key={editing?.id ?? 'new'} member={editing} branches={branches.data} departments={departments.data} onDone={() => setEditing(null)} />
            )}
          </FormLoader>
        )}
      </div>
    </div>
  );
}

function StaffEditor({ member, branches, departments, onDone }: { member: StaffMember | null; branches: Branch[]; departments: Department[]; onDone: () => void }) {
  const create = useCreateStaff();
  const update = useUpdateStaff();
  const {
    register,
    control,
    handleSubmit,
    setValue,
    setError,
    formState: { errors },
  } = useForm<StaffForm>({
    resolver: zodResolver(staffSchema),
    defaultValues: {
      name: member?.name ?? '',
      role: member?.role ?? Role.Cashier,
      scopeType: member?.scopeType ?? ScopeType.Tenant,
      branchId: member?.scopeType === ScopeType.Branch ? (member.scopeId ?? '') : '',
      departmentId: member?.scopeType === ScopeType.Department ? (member.scopeId ?? '') : '',
      pin: '',
      isActive: member?.isActive ?? true,
    },
  });
  const roleValue = Number(useWatch({ control, name: 'role' }));
  const scopeType = Number(useWatch({ control, name: 'scopeType' }));

  const submit = handleSubmit((v) => {
    const scope = scopeFields(v.scopeType, v.branchId, v.departmentId, departments);
    if (!scope.ok) {
      setError(v.scopeType === ScopeType.Branch ? 'branchId' : 'departmentId', { message: scope.message });
      return;
    }
    if (member) {
      update.mutate(
        { id: member.id, body: { role: v.role, scopeType: v.scopeType, scopeId: scope.scopeId, branchId: scope.branchId, isActive: v.isActive } },
        { onSuccess: () => { toast.success('Staff member updated'); onDone(); } },
      );
      return;
    }
    const problem = pinProblem(v.pin);
    if (problem) {
      setError('pin', { message: problem });
      return;
    }
    create.mutate(
      { name: v.name, role: v.role, scopeType: v.scopeType, scopeId: scope.scopeId, branchId: scope.branchId, pin: v.pin.trim() },
      { onSuccess: () => { toast.success('Staff member added'); onDone(); } },
    );
  });

  return (
    <EditorCard title="staff member" editing={!!member} busy={create.isPending || update.isPending} onSubmit={submit} onCancel={onDone}>
      {member ? (
        <p className="text-base font-semibold">{member.name}</p>
      ) : (
        <FormField label="Name" error={errors.name?.message}>
          <input {...register('name')} className={controlClass} />
        </FormField>
      )}

      <FormField label="Role" error={errors.role?.message}>
        <select
          {...register('role', {
            valueAsNumber: true,
            onChange: (e) => {
              // An admin is always whole-business.
              if (Number(e.target.value) === Role.Admin) setValue('scopeType', ScopeType.Tenant);
            },
          })}
          className={controlClass}
        >
          {STAFF_ROLES.map((r) => (
            <option key={r} value={r}>
              {roleLabels[r]}
            </option>
          ))}
        </select>
      </FormField>

      <FormField label="Can work in">
        <select {...register('scopeType', { valueAsNumber: true })} className={controlClass}>
          {scopeChoicesFor(roleValue).map((s) => (
            <option key={s} value={s}>
              {scopeLabels[s]}
            </option>
          ))}
        </select>
      </FormField>

      {scopeType === ScopeType.Branch && (
        <FormField label="Branch" error={errors.branchId?.message}>
          <select {...register('branchId')} className={controlClass}>
            <option value="">Choose a branch</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </FormField>
      )}

      {scopeType === ScopeType.Department && (
        <FormField label="Department" error={errors.departmentId?.message}>
          <select {...register('departmentId')} className={controlClass}>
            <option value="">Choose a department</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name} ({branches.find((b) => b.id === d.branchId)?.name ?? 'branch'})
              </option>
            ))}
          </select>
        </FormField>
      )}

      {!member && (
        <FormField label="PIN" hint="4 to 8 digits. Each person needs a PIN nobody else uses." error={errors.pin?.message}>
          <input type="password" inputMode="numeric" autoComplete="new-password" {...register('pin')} className={controlClass} />
        </FormField>
      )}

      {member && (
        <label className="flex h-12 items-center gap-3 text-base font-semibold">
          <input type="checkbox" {...register('isActive')} className="size-6 accent-brand" />
          Active
        </label>
      )}
    </EditorCard>
  );
}
