import { useState } from 'react';
import { PageHeader } from '../../../components/PageHeader';
import { PrimaryButton } from '../../../components/forms/FormField';
import { useSession } from '../../auth/useSession';
import { useSelectableBranches } from '../../branches/queries';
import { PurchaseRequestDialog } from '../components/PurchaseRequestDialog';
import { PurchaseRequestsPanel } from '../components/PurchaseRequestsPanel';

/** Restock needs and anything else to buy, before a supplier or a price is chosen. Approved requests become purchase orders. */
export function PurchaseRequestsPage() {
  const { role } = useSession();
  const canCreate = role === 'Admin' || role === 'Manager' || role === 'Warehouse';
  const { branches } = useSelectableBranches();
  const [creating, setCreating] = useState(false);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Purchase requests"
        subtitle="What to buy, approved here and turned into orders per supplier"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          canCreate ? (
            <PrimaryButton type="button" onClick={() => setCreating(true)} disabled={!branches}>
              New purchase request
            </PrimaryButton>
          ) : undefined
        }
      />
      <PurchaseRequestsPanel />
      {creating && branches && <PurchaseRequestDialog branches={branches} onClose={() => setCreating(false)} />}
    </div>
  );
}
