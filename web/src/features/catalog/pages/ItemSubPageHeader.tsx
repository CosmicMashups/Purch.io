import { PageHeader } from '../../../components/PageHeader';
import { useItems } from '../queries';

export function ItemSubPageHeader({ itemId, title }: { itemId: string; title: string }) {
  const { data: items } = useItems();
  const item = items?.find((i) => i.id === itemId);

  return (
    <div className="mb-4">
      <PageHeader title={`${title}${item ? ` — ${item.name}` : ''}`} backTo={{ to: '/catalog/items', label: 'Items' }} />
    </div>
  );
}
