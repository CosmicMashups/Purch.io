import { useState } from 'react';
import { ConfirmModal } from '../../components/ConfirmModal';
import { toast } from '../../components/feedback/toastStore';
import { EditorCard } from '../../components/forms/EditorCard';
import { FormField, controlClass } from '../../components/forms/FormField';
import { FormLoader } from '../../components/forms/FormLoader';
import { ListCard, Pill, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime } from '../../lib/dates';
import { useSession } from '../auth/useSession';
import { useBranches } from '../branches/queries';
import type { Branch } from '../branches/types';
import { InviteLinkDialog } from './components/InviteLinkDialog';
import type { Invite, InviteLink, LegacyStaff, Member } from './memberApi';
import { useCancelInvite, useInvite, useInvites, useLegacyStaff, useMembers, useResetLink, useUpdateMember } from './memberQueries';
import { DUTIES, MembershipRole, StaffDuty, describeDuties, dutyLabels, labelOf, membershipRoleLabels } from './types';

const linkButton = 'h-12 text-base font-semibold text-brand-strong underline';
const dangerLink = 'h-12 text-base font-semibold text-danger underline';

function branchNames(ids: string[], branches: Branch[]): string {
  return ids.map((id) => branches.find((b) => b.id === id)?.name ?? 'a branch').join(', ');
}

function describeAccess(person: { role: number; duties: number; branchIds: string[] }, branches: Branch[]): string {
  if (person.role !== MembershipRole.Staff) return `${labelOf(membershipRoleLabels, person.role)}, every branch`;
  return `${describeDuties(person.duties)} at ${branchNames(person.branchIds, branches) || 'no branch'}`;
}

export function StaffPage() {
  const { role } = useSession();
  const isAdmin = role === 'Admin';
  const members = useMembers();
  const invites = useInvites();
  const legacy = useLegacyStaff();
  const branches = useBranches();
  const cancel = useCancelInvite();
  const resetLink = useResetLink();
  const [editing, setEditing] = useState<Member | null>(null);
  const [reEnrol, setReEnrol] = useState<LegacyStaff | null>(null);
  const [shown, setShown] = useState<InviteLink | null>(null);
  const [cancelFor, setCancelFor] = useState<Invite | null>(null);

  // A Manager looks after staff only; the server refuses the rest, so the buttons are simply not offered.
  const mayManage = (member: Member) => isAdmin || member.role === MembershipRole.Staff;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Staff" subtitle="Invite people with a link. They set their own password and PIN." backTo={{ to: '/business', label: 'Business' }} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
        <div className="flex flex-col gap-6">
          <QueryList
            query={members}
            errorTitle="Staff could not be loaded"
            emptyMessage="No one here yet. Invite your first person."
            renderRow={(member) => (
              <ListCard key={member.id}>
                <div className="min-w-0">
                  <p className="text-base font-semibold">{member.name}</p>
                  <p className="text-sm text-ink-soft">{member.email}</p>
                  <p className="text-base">{describeAccess(member, branches.data ?? [])}</p>
                  {mayManage(member) && (
                    <div className="mt-2 flex flex-wrap gap-x-5">
                      <button type="button" onClick={() => setEditing(member)} className={linkButton}>
                        Edit
                      </button>
                      <button type="button" onClick={() => resetLink.mutate(member.id, { onSuccess: setShown })} className={linkButton}>
                        Password reset link
                      </button>
                    </div>
                  )}
                </div>
                {!member.isActive && <Pill>Inactive</Pill>}
              </ListCard>
            )}
          />

          {(legacy.data?.length ?? 0) > 0 && (
            <section aria-labelledby="legacy-heading" className="flex flex-col gap-2">
              <h2 id="legacy-heading" className="text-lg font-semibold">
                Still to invite
              </h2>
              <p className="text-sm text-ink-soft">These people were on the old sign-in. Invite each with an email so they can sign in again; their earlier sales stay theirs.</p>
              {legacy.data?.map((person) => (
                <ListCard key={person.id}>
                  <div className="min-w-0">
                    <p className="text-base font-semibold">{person.name}</p>
                    <button type="button" onClick={() => { setEditing(null); setReEnrol(person); }} className={linkButton}>
                      Invite
                    </button>
                  </div>
                </ListCard>
              ))}
            </section>
          )}

          {(invites.data?.length ?? 0) > 0 && (
            <section aria-labelledby="pending-heading" className="flex flex-col gap-2">
              <h2 id="pending-heading" className="text-lg font-semibold">
                Waiting to join
              </h2>
              {invites.data?.map((invite) => (
                <ListCard key={invite.id}>
                  <div className="min-w-0">
                    <p className="text-base font-semibold">{invite.name || invite.email}</p>
                    <p className="text-sm text-ink-soft">
                      {invite.purpose === 1 ? 'Password reset' : describeAccess(invite, branches.data ?? [])}, link expires {formatDateTime(invite.expiresAt)}
                    </p>
                    <button type="button" onClick={() => setCancelFor(invite)} className={dangerLink}>
                      Cancel link
                    </button>
                  </div>
                </ListCard>
              ))}
            </section>
          )}
        </div>

        <FormLoader failed={branches.isError ? branches : null} ready={!!branches.data}>
          {branches.data && <PersonEditor key={editing?.id ?? reEnrol?.id ?? 'new'} member={editing} legacy={reEnrol} branches={branches.data} isAdmin={isAdmin} onDone={() => { setEditing(null); setReEnrol(null); }} onInvited={(link) => { setReEnrol(null); setShown(link); }} />}
        </FormLoader>
      </div>

      <ConfirmModal
        open={cancelFor !== null}
        destructive
        title="Cancel this link?"
        description="It stops working at once. You can invite the person again."
        confirmLabel="Cancel link"
        onConfirm={() => {
          if (cancelFor) cancel.mutate(cancelFor.id, { onSuccess: () => toast.info('Link cancelled') });
          setCancelFor(null);
        }}
        onCancel={() => setCancelFor(null)}
      />
      {shown && <InviteLinkDialog link={shown} onClose={() => setShown(null)} />}
    </div>
  );
}

function PersonEditor({ member, legacy, branches, isAdmin, onDone, onInvited }: { member: Member | null; legacy: LegacyStaff | null; branches: Branch[]; isAdmin: boolean; onDone: () => void; onInvited: (link: InviteLink) => void }) {
  const invite = useInvite();
  const update = useUpdateMember();
  const [name, setName] = useState(legacy?.name ?? '');
  const [email, setEmail] = useState('');
  // An Admin's old account can only come back as an Admin; a Manager can only invite staff.
  const [role, setRole] = useState<number>(member?.role ?? (legacy && isAdmin ? legacy.suggestedRole : MembershipRole.Staff));
  const [duties, setDuties] = useState<number>(member?.duties ?? (legacy && legacy.suggestedDuties !== 0 ? legacy.suggestedDuties : StaffDuty.Cashier));
  const [branchIds, setBranchIds] = useState<string[]>(member?.branchIds ?? (legacy?.branchId ? [legacy.branchId] : branches.length === 1 ? [branches[0].id] : []));
  const [isActive, setIsActive] = useState(member?.isActive ?? true);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const isStaff = role === MembershipRole.Staff;
  const toggleDuty = (duty: number) => setDuties((d) => d ^ duty);
  const toggleBranch = (id: string) => setBranchIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const found: Record<string, string> = {};
    if (!member && !name.trim()) found.name = 'Enter their name';
    if (!member && !/^\S+@\S+\.\S+$/.test(email.trim())) found.email = 'Enter a valid email address';
    if (isStaff && duties === 0) found.duties = 'Choose at least one duty';
    if (isStaff && branchIds.length === 0) found.branches = 'Choose at least one branch';
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    const access = { role, duties: isStaff ? duties : 0, branchIds: isStaff ? branchIds : [] };
    if (member) {
      update.mutate({ id: member.id, body: { ...access, isActive } }, { onSuccess: () => { toast.success('Saved'); onDone(); } });
      return;
    }
    invite.mutate({ name: name.trim(), email: email.trim(), ...access, legacyUserId: legacy?.id ?? null }, { onSuccess: onInvited });
    setName('');
    setEmail('');
  }

  return (
    <EditorCard title="person" editing={!!member} heading={member ? `Edit ${member.name}` : legacy ? `Invite ${legacy.name}` : 'Invite someone'} submitLabel={member ? 'Save' : 'Invite'} busy={invite.isPending || update.isPending} onSubmit={submit} onCancel={onDone}>
      {!member && (
        <>
          <FormField label="Name" error={errors.name}>
            <input value={name} onChange={(e) => setName(e.target.value)} className={controlClass} />
          </FormField>
          <FormField label="Email" hint="Their login name. No email is sent." error={errors.email}>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="off" className={controlClass} />
          </FormField>
        </>
      )}

      <FormField label="Role">
        <select value={role} onChange={(e) => setRole(Number(e.target.value))} className={controlClass} disabled={!isAdmin}>
          {(isAdmin ? [MembershipRole.Staff, MembershipRole.Manager, MembershipRole.Admin] : [MembershipRole.Staff]).map((r) => (
            <option key={r} value={r}>
              {membershipRoleLabels[r]}
            </option>
          ))}
        </select>
      </FormField>

      {isStaff && (
        <>
          <fieldset className="flex flex-col gap-1">
            <legend className="text-base font-semibold">Can work as</legend>
            {DUTIES.map((duty) => (
              <label key={duty} className="flex h-12 items-center gap-3 text-base">
                <input type="checkbox" checked={(duties & duty) !== 0} onChange={() => toggleDuty(duty)} className="size-6 accent-brand" />
                {dutyLabels[duty]}
              </label>
            ))}
            {errors.duties && <p role="alert" className="text-sm font-medium text-danger">{errors.duties}</p>}
          </fieldset>
          <fieldset className="flex flex-col gap-1">
            <legend className="text-base font-semibold">Works at</legend>
            {branches.map((b) => (
              <label key={b.id} className="flex h-12 items-center gap-3 text-base">
                <input type="checkbox" checked={branchIds.includes(b.id)} onChange={() => toggleBranch(b.id)} className="size-6 accent-brand" />
                {b.name}
              </label>
            ))}
            {errors.branches && <p role="alert" className="text-sm font-medium text-danger">{errors.branches}</p>}
          </fieldset>
        </>
      )}

      {member && (
        <label className="flex h-12 items-center gap-3 text-base font-semibold">
          <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} className="size-6 accent-brand" />
          Active
        </label>
      )}
    </EditorCard>
  );
}
