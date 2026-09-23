import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SearchBar } from '../SearchBar';

describe('SearchBar', () => {
  it('renders input with value and default placeholder', () => {
    render(<SearchBar value="iced latte" onChange={() => {}} />);
    const input = screen.getByRole('searchbox') as HTMLInputElement;
    expect(input).toBeInTheDocument();
    expect(input.value).toBe('iced latte');
    expect(input.placeholder).toBe('Search…');
  });

  it('renders custom placeholder', () => {
    render(
      <SearchBar
        value=""
        onChange={() => {}}
        placeholder="Filter categories..."
      />
    );
    const input = screen.getByPlaceholderText('Filter categories...');
    expect(input).toBeInTheDocument();
  });

  it('triggers onChange callback when value changes', () => {
    const handleChange = vi.fn();
    render(<SearchBar value="" onChange={handleChange} />);
    const input = screen.getByRole('searchbox');

    fireEvent.change(input, { target: { value: 'croissant' } });
    expect(handleChange).toHaveBeenCalledWith('croissant');
  });
});
