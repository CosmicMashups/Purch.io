import { fireEvent, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage } from '../test/render';
import { DEFAULT_CONFIG, useHardwareConfig } from './config';
import { HardwarePage } from './HardwarePage';
import { useScale } from './scale/scaleStore';

const connect = vi.fn().mockResolvedValue(undefined);
const disconnect = vi.fn().mockResolvedValue(undefined);

function setSerial(present: boolean) {
  if (present) Object.defineProperty(navigator, 'serial', { value: { requestPort: vi.fn() }, configurable: true });
  else Reflect.deleteProperty(navigator, 'serial');
}

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.clear();
  useHardwareConfig.setState({ ...DEFAULT_CONFIG });
  useScale.setState({ status: 'disconnected', reading: null, message: null, scale: null, connect, disconnect });
});
afterEach(() => setSerial(false));

describe('HardwarePage', () => {
  it('explains what a browser cannot do, instead of pretending', () => {
    renderPage(<HardwarePage />);
    expect(screen.getByRole('heading', { name: 'Cash drawer and direct printer commands' })).toBeInTheDocument();
    expect(screen.getByText(/browsers cannot send those commands/i)).toBeInTheDocument();
  });

  it('disables scale connection where Web Serial is missing', () => {
    setSerial(false);
    renderPage(<HardwarePage />);
    expect(screen.getByRole('button', { name: 'Connect scale' })).toBeDisabled();
    expect(screen.getByText(/needs Chrome or Edge/i)).toBeInTheDocument();
  });

  it('connects a scale when the browser can', () => {
    setSerial(true);
    renderPage(<HardwarePage />);
    fireEvent.click(screen.getByRole('button', { name: 'Connect scale' }));
    expect(connect).toHaveBeenCalled();
  });

  it('shows the live panel and a disconnect button while connected', () => {
    setSerial(true);
    useScale.setState({ status: 'connected' });
    renderPage(<HardwarePage />);
    expect(screen.getByLabelText('Scale')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Disconnect scale' }));
    expect(disconnect).toHaveBeenCalled();
  });

  it('remembers the scale type, speed and paper width', () => {
    renderPage(<HardwarePage />);
    fireEvent.change(screen.getByLabelText('Scale type'), { target: { value: 'mettlerToledo' } });
    fireEvent.change(screen.getByLabelText('Speed (baud)'), { target: { value: '19200' } });
    fireEvent.change(screen.getByLabelText('Paper width'), { target: { value: 'mm58' } });
    expect(useHardwareConfig.getState()).toMatchObject({ scaleProtocol: 'mettlerToledo', scaleBaudRate: 19200, paperWidth: 'mm58' });
    expect(JSON.parse(window.localStorage.getItem('purch.hardware') ?? '{}').paperWidth).toBe('mm58');
  });

  it('locks the scale settings while a scale is connected', () => {
    useScale.setState({ status: 'connected' });
    renderPage(<HardwarePage />);
    expect(screen.getByLabelText('Scale type')).toBeDisabled();
  });

  it('shows a scale problem plainly', () => {
    useScale.setState({ status: 'error', message: 'Port busy' });
    renderPage(<HardwarePage />);
    expect(screen.getByRole('alert')).toHaveTextContent('Port busy');
  });
});
