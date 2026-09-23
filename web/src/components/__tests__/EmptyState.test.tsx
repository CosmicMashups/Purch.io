import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { EmptyState } from '../EmptyState';

describe('EmptyState', () => {
  it('renders title', () => {
    render(<EmptyState title="No items found" />);
    expect(screen.getByText('No items found')).toBeInTheDocument();
  });

  it('renders description when provided', () => {
    render(
      <EmptyState
        title="No categories"
        description="Create your first category to get started."
      />
    );
    expect(screen.getByText('No categories')).toBeInTheDocument();
    expect(
      screen.getByText('Create your first category to get started.')
    ).toBeInTheDocument();
  });

  it('renders action element and handles interaction', () => {
    const handleClick = vi.fn();
    render(
      <EmptyState
        title="No items"
        action={<button onClick={handleClick}>Add Item</button>}
      />
    );

    const button = screen.getByRole('button', { name: 'Add Item' });
    expect(button).toBeInTheDocument();
    fireEvent.click(button);
    expect(handleClick).toHaveBeenCalledTimes(1);
  });
});
