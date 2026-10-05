import type { CSSProperties, ReactNode } from 'react';
import { DndContext, KeyboardSensor, PointerSensor, closestCenter, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core';
import { SortableContext, arrayMove, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

export interface SortableColumn<T> {
  header: string;
  className?: string;
  cell: (row: T) => ReactNode;
}

export interface SortableGroup<T> {
  id: string;
  title: string;
  rows: T[];
}

/**
 * A table whose rows are grouped under a heading each. Rows can be dragged by their handle (or moved with the
 * keyboard) but only within their own group; a drop reports that group's new order of row ids.
 */
export function SortableGroupedTable<T>({
  groups,
  columns,
  getId,
  rowLabel,
  onReorder,
  disabled = false,
}: {
  groups: SortableGroup<T>[];
  columns: SortableColumn<T>[];
  getId: (row: T) => string;
  rowLabel: (row: T) => string;
  onReorder: (orderedIds: string[]) => void;
  /** Turns the handles off, for example while a search hides part of a group. */
  disabled?: boolean;
}) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }), useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }));

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
      <table className="min-w-full text-sm">
        <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
          <tr>
            <th className="w-10 px-2 py-2">
              <span className="sr-only">Reorder</span>
            </th>
            {columns.map((column) => (
              <th key={column.header} className={`px-4 py-2 ${column.className ?? ''}`}>
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        {groups.map((group) => {
          const ids = group.rows.map(getId);
          const handleDragEnd = ({ active, over }: DragEndEvent) => {
            if (!over || active.id === over.id) return;
            const from = ids.indexOf(String(active.id));
            const to = ids.indexOf(String(over.id));
            if (from < 0 || to < 0) return;
            onReorder(arrayMove(ids, from, to));
          };
          return (
            <DndContext key={group.id} sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
              <SortableContext items={ids} strategy={verticalListSortingStrategy}>
                <tbody className="divide-y divide-gray-100 border-t border-gray-200">
                  <tr className="bg-gray-50">
                    <th colSpan={columns.length + 1} scope="colgroup" className="px-4 py-1.5 text-left text-xs font-semibold uppercase tracking-wide text-gray-600">
                      {group.title} <span className="font-normal text-gray-400">({group.rows.length})</span>
                    </th>
                  </tr>
                  {group.rows.map((row) => (
                    <SortableRow key={getId(row)} id={getId(row)} label={rowLabel(row)} disabled={disabled || group.rows.length < 2}>
                      {columns.map((column) => (
                        <td key={column.header} className={`px-4 py-2 ${column.className ?? ''}`}>
                          {column.cell(row)}
                        </td>
                      ))}
                    </SortableRow>
                  ))}
                </tbody>
              </SortableContext>
            </DndContext>
          );
        })}
      </table>
    </div>
  );
}

function SortableRow({ id, label, disabled, children }: { id: string; label: string; disabled: boolean; children: ReactNode }) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({ id, disabled });
  const style: CSSProperties = { transform: CSS.Transform.toString(transform), transition, position: 'relative', zIndex: isDragging ? 1 : undefined };
  return (
    <tr ref={setNodeRef} style={style} className={isDragging ? 'bg-white shadow-md' : undefined}>
      <td className="w-10 px-2 py-2 align-middle">
        <button
          type="button"
          ref={setActivatorNodeRef}
          {...attributes}
          {...listeners}
          disabled={disabled}
          aria-label={`Drag to reorder ${label}`}
          className="flex size-8 touch-none cursor-grab items-center justify-center rounded text-gray-400 hover:bg-gray-100 hover:text-gray-700 active:cursor-grabbing disabled:cursor-not-allowed disabled:opacity-30"
        >
          <svg viewBox="0 0 20 20" className="size-4" fill="currentColor" aria-hidden="true">
            {[5, 10, 15].flatMap((y) => [7, 13].map((x) => <circle key={`${x}-${y}`} cx={x} cy={y} r="1.5" />))}
          </svg>
        </button>
      </td>
      {children}
    </tr>
  );
}
