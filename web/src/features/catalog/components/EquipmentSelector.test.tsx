import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { EquipmentKind, EquipmentStatus, type Equipment } from '../../equipment/types';
import { EquipmentSelector } from './EquipmentSelector';

const make = (id: string, name: string, status: EquipmentStatus = EquipmentStatus.Operational): Equipment => ({
  id,
  name,
  kind: EquipmentKind.Equipment,
  status,
  quantity: null,
  location: null,
  notes: null,
  isActive: true,
  sortOrder: 0,
  usedByItemCount: 0,
});

const fryer = make('fryer', 'Deep fryer');
const machine = make('machine', 'Ice cream machine', EquipmentStatus.OutOfService);

function Harness({ initial = [] as string[], onChange }: { initial?: string[]; onChange?: (ids: string[]) => void }) {
  const [selected, setSelected] = useState(initial);
  return (
    <EquipmentSelector
      equipment={[fryer, machine]}
      selected={selected}
      onChange={(ids) => {
        setSelected(ids);
        onChange?.(ids);
      }}
    />
  );
}

describe('EquipmentSelector', () => {
  it('ticks and removes equipment', () => {
    const seen: string[][] = [];
    render(<Harness onChange={(ids) => seen.push(ids)} />);
    fireEvent.focus(screen.getByRole('combobox'));
    fireEvent.click(screen.getByRole('option', { name: /Deep fryer/ }));

    expect(seen.at(-1)).toEqual(['fryer']);
    fireEvent.click(screen.getByRole('button', { name: 'Remove Deep fryer' }));
    expect(seen.at(-1)).toEqual([]);
  });

  it('narrows the list as you type', () => {
    render(<Harness />);
    const box = screen.getByRole('combobox');
    fireEvent.focus(box);
    fireEvent.change(box, { target: { value: 'ice' } });

    expect(screen.getByRole('option', { name: /Ice cream machine/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Deep fryer/ })).not.toBeInTheDocument();
  });

  it('warns that an out-of-service machine makes this item show as out of stock', () => {
    render(<Harness initial={['machine']} />);
    expect(screen.getByText(/shows as out of stock/)).toBeInTheDocument();
  });
});
