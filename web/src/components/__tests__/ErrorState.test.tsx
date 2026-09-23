import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ErrorState, describeQueryError } from '../ErrorState';

describe('ErrorState', () => {
  it('renders default title when no title is provided', () => {
    render(<ErrorState />);
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('renders custom title and message', () => {
    render(
      <ErrorState
        title="Failed to load catalog"
        message="Network timeout while connecting to server"
      />
    );
    expect(screen.getByText('Failed to load catalog')).toBeInTheDocument();
    expect(
      screen.getByText('Network timeout while connecting to server')
    ).toBeInTheDocument();
  });

  it('renders retry button and calls onRetry when clicked', () => {
    const handleRetry = vi.fn();
    render(<ErrorState onRetry={handleRetry} />);

    const button = screen.getByRole('button', { name: 'Try again' });
    expect(button).toBeInTheDocument();

    fireEvent.click(button);
    expect(handleRetry).toHaveBeenCalledTimes(1);
  });

  it('does not render retry button when onRetry is omitted', () => {
    render(<ErrorState />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

describe('describeQueryError', () => {
  it('extracts message property from an Error object', () => {
    const err = new Error('Server returned 500');
    expect(describeQueryError(err)).toBe('Server returned 500');
  });

  it('extracts message property from a plain object with message', () => {
    const err = { message: 'Custom error message' };
    expect(describeQueryError(err)).toBe('Custom error message');
  });

  it('returns undefined for non-object errors or missing message', () => {
    expect(describeQueryError(null)).toBeUndefined();
    expect(describeQueryError(undefined)).toBeUndefined();
    expect(describeQueryError('string error')).toBeUndefined();
    expect(describeQueryError(123)).toBeUndefined();
    expect(describeQueryError({ status: 500 })).toBeUndefined();
  });
});
