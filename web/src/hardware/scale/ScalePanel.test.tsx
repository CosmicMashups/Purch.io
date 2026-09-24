import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { parseCas } from './parser';
import { ScalePanel } from './ScalePanel';
import { useScale } from './scaleStore';

const zero = vi.fn().mockResolvedValue(undefined);
const tare = vi.fn().mockResolvedValue(undefined);

beforeEach(() => {
  vi.clearAllMocks();
  useScale.setState({ status: 'connected', reading: null, message: null, scale: null, zero, tare });
});

describe('ScalePanel', () => {
  it('waits when the scale has said nothing', () => {
    render(<ScalePanel onUse={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent('No reading');
    expect(screen.getByRole('button', { name: /waiting for a stable weight/i })).toBeDisabled();
  });

  it('offers a settled weight in kilograms', () => {
    useScale.setState({ reading: parseCas('ST,GS,  1.250kg') });
    const onUse = vi.fn();
    render(<ScalePanel onUse={onUse} />);
    expect(screen.getByRole('status')).toHaveTextContent('Stable');
    fireEvent.click(screen.getByRole('button', { name: 'Use 1.250 kg' }));
    expect(onUse).toHaveBeenCalledWith(1.25);
  });

  it('converts grams before offering them', () => {
    useScale.setState({ reading: parseCas('ST,GS,  250g') });
    render(<ScalePanel onUse={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Use 0.250 kg' })).toBeEnabled();
  });

  it('will not lock in a weight that is still moving', () => {
    useScale.setState({ reading: parseCas('US,GS,  1.250kg') });
    render(<ScalePanel onUse={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent('Settling');
    expect(screen.getByRole('button', { name: /waiting for a stable weight/i })).toBeDisabled();
  });

  it('flags an overload', () => {
    useScale.setState({ reading: parseCas('OL,GS,') });
    render(<ScalePanel onUse={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent('Overload');
  });

  it('sends zero and tare', () => {
    render(<ScalePanel />);
    fireEvent.click(screen.getByRole('button', { name: 'Zero' }));
    fireEvent.click(screen.getByRole('button', { name: 'Tare' }));
    expect(zero).toHaveBeenCalled();
    expect(tare).toHaveBeenCalled();
  });

  it('has no use button when nothing asked for the weight', () => {
    render(<ScalePanel />);
    expect(screen.queryByRole('button', { name: /use|waiting/i })).toBeNull();
  });
});
