import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { StaleDataNotice } from './StaleDataNotice';

function setOnline(value: boolean) {
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(value);
}
afterEach(() => vi.restoreAllMocks());

describe('StaleDataNotice', () => {
  it('says the data may be out of date while offline', () => {
    setOnline(false);
    render(<StaleDataNotice updatedAt={Date.now()} what="items" />);
    expect(screen.getByRole('status')).toHaveTextContent(/showing items saved on/i);
    expect(screen.getByRole('status')).toHaveTextContent(/out of date/i);
  });

  it('is silent when online', () => {
    setOnline(true);
    render(<StaleDataNotice updatedAt={Date.now()} what="items" />);
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('is silent when nothing was ever loaded', () => {
    setOnline(false);
    render(<StaleDataNotice updatedAt={0} what="items" />);
    expect(screen.queryByRole('status')).toBeNull();
  });
});
