import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { OpenItemDialog } from './OpenItemDialog';

describe('OpenItemDialog', () => {
  it('validates the name, price and quantity', () => {
    const onAdd = vi.fn();
    render(<OpenItemDialog busy={false} onAdd={onAdd} onClose={() => {}} />);
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add to cart' }));
    expect(screen.getByText('Name the item')).toBeInTheDocument();
    expect(screen.getByText('Enter a price')).toBeInTheDocument();
    expect(screen.getByText('Quantity must be more than 0')).toBeInTheDocument();
    expect(onAdd).not.toHaveBeenCalled();
  });

  it('sends the trimmed item', () => {
    const onAdd = vi.fn();
    render(<OpenItemDialog busy={false} onAdd={onAdd} onClose={() => {}} />);
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: '  Gift wrap ' } });
    fireEvent.change(screen.getByLabelText('Price'), { target: { value: '25.50' } });
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText(/Note/), { target: { value: 'Red paper' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add to cart' }));
    expect(onAdd).toHaveBeenCalledWith({ name: 'Gift wrap', unitPrice: 25.5, quantity: 2, note: 'Red paper' });
  });
});
