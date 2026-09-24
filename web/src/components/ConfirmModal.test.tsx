import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ConfirmModal } from './ConfirmModal';

const base = { title: 'Cancel this order?', description: 'It cannot be undone.', confirmLabel: 'Cancel order', onConfirm: vi.fn(), onCancel: vi.fn() };

describe('ConfirmModal', () => {
  it('renders nothing while closed', () => {
    render(<ConfirmModal {...base} open={false} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('is an accessible dialog and starts focus on Cancel', () => {
    render(<ConfirmModal {...base} open />);
    expect(screen.getByRole('dialog', { name: 'Cancel this order?' })).toHaveAccessibleDescription('It cannot be undone.');
    expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus();
  });

  it('confirms and cancels through their buttons', () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(<ConfirmModal {...base} open onConfirm={onConfirm} onCancel={onCancel} />);
    fireEvent.click(screen.getByRole('button', { name: 'Cancel order' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('backs out on Escape without confirming', () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(<ConfirmModal {...base} open onConfirm={onConfirm} onCancel={onCancel} />);
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('disables confirm while busy', () => {
    render(<ConfirmModal {...base} open busy />);
    expect(screen.getByRole('button', { name: 'Cancel order' })).toBeDisabled();
  });
});
