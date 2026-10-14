import type { Branch } from '../../branches/types';

/** The branch's manual GCash QR and account, for the customer to scan. Shared by single and split payment. */
export function GcashQr({ branch }: { branch: Branch | undefined }) {
  return (
    <>
      {branch?.manualGcashQrImageUrl ? (
        <img src={branch.manualGcashQrImageUrl} alt="GCash QR code for this branch" className="size-56 rounded-control border border-line object-contain" />
      ) : (
        <p className="rounded-control border border-dashed border-ink-soft/40 p-4 text-base text-ink-soft">No QR is set for this branch. An admin can add one in the branch settings.</p>
      )}
      {(branch?.manualGcashAccountName || branch?.manualGcashAccountNumber) && (
        <p className="text-base font-semibold">{[branch.manualGcashAccountName, branch.manualGcashAccountNumber].filter(Boolean).join(', ')}</p>
      )}
    </>
  );
}
