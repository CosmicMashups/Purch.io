import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PurchImage } from './PurchImage';

describe('PurchImage', () => {
  it('falls back, then to the error node, when a picture will not load', () => {
    render(<PurchImage src="https://example.test/a.png" fallback="https://example.test/b.png" alt="pic" errorNode={<span>none</span>} />);
    expect(screen.getByAltText('pic')).toHaveAttribute('src', 'https://example.test/a.png');
    fireEvent.error(screen.getByAltText('pic'));
    expect(screen.getByAltText('pic')).toHaveAttribute('src', 'https://example.test/b.png');
    fireEvent.error(screen.getByAltText('pic'));
    expect(screen.getByText('none')).toBeInTheDocument();
  });

  it('draws the error node when there is no value and no fallback', () => {
    render(<PurchImage src={null} alt="pic" errorNode={<span>none</span>} />);
    expect(screen.getByText('none')).toBeInTheDocument();
  });
});
