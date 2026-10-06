import { fireEvent, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { GroupedCombobox, type ComboboxGroup } from './GroupedCombobox';

const groups: ComboboxGroup[] = [
  { label: 'Items · Drinks', options: [{ value: 'item:latte', label: 'Latte' }] },
  {
    label: 'Ingredients · Dairy',
    options: [
      { value: 'ingredient:milk', label: 'Milk', hint: 'ml', keywords: 'MLK-1' },
      { value: 'ingredient:cream', label: 'Cream', hint: 'ml' },
    ],
  },
];

function Harness({ emptyLabel }: { emptyLabel?: string }) {
  const [value, setValue] = useState('');
  return (
    <>
      <GroupedCombobox groups={groups} value={value} onChange={setValue} emptyLabel={emptyLabel} placeholder="Pick" />
      <output>{value}</output>
    </>
  );
}

describe('GroupedCombobox', () => {
  it('lists options under their group headings when opened', () => {
    render(<Harness />);
    fireEvent.click(screen.getByRole('combobox'));
    expect(screen.getByText('Items · Drinks')).toBeInTheDocument();
    expect(screen.getByText('Ingredients · Dairy')).toBeInTheDocument();
    expect(screen.getAllByRole('option')).toHaveLength(3);
  });

  it('narrows by typing, matching the SKU too, and drops headings with nothing left', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox');
    fireEvent.change(input, { target: { value: 'mlk' } });
    expect(within(screen.getByRole('listbox')).getAllByRole('option')).toHaveLength(1);
    expect(screen.queryByText('Items · Drinks')).not.toBeInTheDocument();
    fireEvent.change(input, { target: { value: 'zzz' } });
    expect(screen.getByText('Nothing matches')).toBeInTheDocument();
  });

  it('picks with the keyboard and shows the choice in the box', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox');
    fireEvent.click(input);
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(screen.getByRole('status')).toHaveTextContent('ingredient:cream');
    expect(input).toHaveValue('Cream');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('offers a clear option for filters', () => {
    render(<Harness emptyLabel="Everything" />);
    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.click(screen.getByRole('option', { name: 'Latte' }));
    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.click(screen.getByRole('option', { name: 'Everything' }));
    expect(screen.getByRole('status')).toHaveTextContent('');
  });
});
