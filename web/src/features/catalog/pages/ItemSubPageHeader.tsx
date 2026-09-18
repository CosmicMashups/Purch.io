import { Link } from 'react-router-dom';
import { useItems } from '../queries';

export function ItemSubPageHeader({ itemId, title }: { itemId: string; title: string }) {
  const { data: items } = useItems();
  const item = items?.find((i) => i.id === itemId);

  return (
    <div className="mb-4 flex flex-col gap-1">
      <Link to="/catalog/items" className="text-xs text-gray-500 hover:underline">
        ← Back to Items
      </Link>
      <h1 className="text-xl font-semibold text-gray-900">
        {title}
        {item ? ` — ${item.name}` : ''}
      </h1>
    </div>
  );
}
