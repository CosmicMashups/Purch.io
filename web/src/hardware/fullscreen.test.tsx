import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FullscreenButton } from './fullscreen';

function stub(enabled: boolean) {
  Object.defineProperty(document, 'fullscreenEnabled', { value: enabled, configurable: true });
  const request = vi.fn().mockResolvedValue(undefined);
  document.documentElement.requestFullscreen = request;
  return request;
}
afterEach(() => Object.defineProperty(document, 'fullscreenElement', { value: null, configurable: true }));

describe('FullscreenButton', () => {
  it('asks the browser for full screen', () => {
    const request = stub(true);
    render(<FullscreenButton />);
    fireEvent.click(screen.getByRole('button', { name: 'Full screen' }));
    expect(request).toHaveBeenCalled();
  });

  it('offers a way back once full screen is on', () => {
    stub(true);
    Object.defineProperty(document, 'fullscreenElement', { value: document.body, configurable: true });
    render(<FullscreenButton />);
    expect(screen.getByRole('button', { name: 'Exit full screen' })).toBeInTheDocument();
  });

  it('is not shown where the browser does not allow it', () => {
    stub(false);
    const { container } = render(<FullscreenButton />);
    expect(container).toBeEmptyDOMElement();
  });
});
