import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CameraScanDialog } from './CameraScanDialog';
import { cameraScanSupported } from './cameraSupport';

const w = window as unknown as { BarcodeDetector?: unknown };

afterEach(() => {
  delete w.BarcodeDetector;
  vi.restoreAllMocks();
});

function stubCamera(getUserMedia: () => Promise<unknown>) {
  Object.defineProperty(navigator, 'mediaDevices', { value: { getUserMedia }, configurable: true });
}

describe('cameraScanSupported', () => {
  it('needs both the detector and the camera', () => {
    stubCamera(() => Promise.resolve({}));
    expect(cameraScanSupported()).toBe(false);
    w.BarcodeDetector = class {};
    expect(cameraScanSupported()).toBe(true);
  });
});

describe('CameraScanDialog', () => {
  it('says so when the browser cannot scan', () => {
    render(<CameraScanDialog onDetect={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByText(/cannot scan with a camera/i)).toBeInTheDocument();
  });

  it('explains a refused camera', async () => {
    w.BarcodeDetector = class {
      detect = () => Promise.resolve([]);
    };
    stubCamera(() => Promise.reject(new Error('denied')));
    render(<CameraScanDialog onDetect={vi.fn()} onClose={vi.fn()} />);
    expect(await screen.findByRole('alert')).toHaveTextContent(/allow camera access/i);
  });

  it('reports the first code it sees and releases the camera', async () => {
    const stop = vi.fn();
    w.BarcodeDetector = class {
      detect = () => Promise.resolve([{ rawValue: '4800016001234' }]);
    };
    stubCamera(() => Promise.resolve({ getTracks: () => [{ stop }] }));
    const onDetect = vi.fn();
    const { unmount } = render(<CameraScanDialog onDetect={onDetect} onClose={vi.fn()} />);
    await waitFor(() => expect(onDetect).toHaveBeenCalledWith('4800016001234'), { timeout: 2000 });
    unmount();
    expect(stop).toHaveBeenCalled();
  });
});
