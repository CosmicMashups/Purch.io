import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { DiscountDialog } from './DiscountDialog';
import { PriceOverrideDialog } from './PriceOverrideDialog';

function setup(over: Partial<React.ComponentProps<typeof DiscountDialog>> = {}) {
  const onApply = vi.fn();
  const onRemove = vi.fn();
  render(<DiscountDialog subject="Iced Latte" total={200} busy={false} onApply={onApply} onRemove={onRemove} onClose={() => {}} {...over} />);
  return { onApply, onRemove };
}

const value = () => screen.getByLabelText(/^Percent off|^Amount off/);

describe('DiscountDialog', () => {
  it('needs a reason before applying', () => {
    const { onApply } = setup();
    fireEvent.change(value(), { target: { value: '10' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply discount' }));
    expect(screen.getByText('Give a reason')).toBeInTheDocument();
    expect(onApply).not.toHaveBeenCalled();
  });

  it('applies a percent discount with a chip reason and previews the amount', () => {
    const { onApply } = setup();
    fireEvent.change(value(), { target: { value: '10' } });
    expect(screen.getByTestId('discount-preview')).toHaveTextContent('20.00');
    fireEvent.click(screen.getByRole('button', { name: 'Loyal customer' }));
    fireEvent.click(screen.getByRole('button', { name: 'Apply discount' }));
    expect(onApply).toHaveBeenCalledWith({ kind: 'Percent', value: 10, reason: 'Loyal customer' });
  });

  it('refuses a percent over 100', () => {
    const { onApply } = setup();
    fireEvent.change(value(), { target: { value: '120' } });
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'r' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply discount' }));
    expect(screen.getByText('A percent discount can be at most 100%')).toBeInTheDocument();
    expect(onApply).not.toHaveBeenCalled();
  });

  it('refuses a fixed amount above the total', () => {
    const { onApply } = setup();
    fireEvent.click(screen.getByRole('radio', { name: /Peso amount/ }));
    fireEvent.change(value(), { target: { value: '250' } });
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'r' } });
    fireEvent.click(screen.getByRole('button', { name: 'Apply discount' }));
    expect(screen.getByText(/Can't be more than/)).toBeInTheDocument();
    expect(onApply).not.toHaveBeenCalled();
  });

  it('removes an existing discount', () => {
    const { onRemove } = setup({ initial: { kind: 'Percent', value: 10, reason: 'Loyal customer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Remove discount' }));
    expect(onRemove).toHaveBeenCalled();
  });
});

describe('PriceOverrideDialog', () => {
  it('needs a reason and sends the new price', () => {
    const onApply = vi.fn();
    render(<PriceOverrideDialog itemName="Iced Latte" currentUnitPrice={150} busy={false} onApply={onApply} onClose={() => {}} />);
    fireEvent.change(screen.getByLabelText('New unit price'), { target: { value: '120' } });
    fireEvent.click(screen.getByRole('button', { name: 'Override price' }));
    expect(onApply).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Dented can' } });
    fireEvent.click(screen.getByRole('button', { name: 'Override price' }));
    expect(onApply).toHaveBeenCalledWith({ newUnitPrice: 120, reason: 'Dented can' });
  });
});
