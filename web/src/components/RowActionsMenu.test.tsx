import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { RowActionsMenu } from './RowActionsMenu';

describe('RowActionsMenu', () => {
  it('keeps its actions hidden until the three-dot button is pressed, and names the row it belongs to', () => {
    render(<RowActionsMenu subject="Add fries & sides" actions={[{ label: 'Edit', onSelect: vi.fn() }]} />);
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();

    const button = screen.getByRole('button', { name: 'Actions for Add fries & sides' });
    expect(button).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(button);

    expect(button).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('menu', { name: 'Actions for Add fries & sides' })).toBeInTheDocument();
  });

  it('runs the chosen action and closes', () => {
    const onEdit = vi.fn();
    render(<RowActionsMenu subject="Coffee" actions={[{ label: 'Edit', onSelect: onEdit }, { label: 'Delete', danger: true, onSelect: vi.fn() }]} />);
    fireEvent.click(screen.getByRole('button', { name: 'Actions for Coffee' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Edit' }));
    expect(onEdit).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('closes on Escape and returns focus to the button', () => {
    render(<RowActionsMenu subject="Coffee" actions={[{ label: 'Edit', onSelect: vi.fn() }]} />);
    const button = screen.getByRole('button', { name: 'Actions for Coffee' });
    fireEvent.click(button);
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(button).toHaveFocus();
  });

  it('does not run a disabled action, and says why it is unavailable', () => {
    const onDelete = vi.fn();
    render(<RowActionsMenu subject="Aling Nena" actions={[{ label: 'Delete', disabled: true, hint: 'Collect the balance first', onSelect: onDelete }]} />);
    fireEvent.click(screen.getByRole('button', { name: 'Actions for Aling Nena' }));
    const item = screen.getByRole('menuitem', { name: /Delete/ });
    expect(item).toBeDisabled();
    expect(item).toHaveTextContent('Collect the balance first');
    fireEvent.click(item);
    expect(onDelete).not.toHaveBeenCalled();
  });

  it('renders nothing when there is nothing to offer', () => {
    render(<RowActionsMenu subject="Coffee" actions={[]} />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
