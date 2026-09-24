import { describe, expect, it, vi } from 'vitest';
import type { ScaleReading } from './parser';
import { SerialScale, takeLines, type ScaleStatus, type SerialPortLike } from './serialScale';

function fakePort() {
  let push!: (text: string) => void;
  let end!: () => void;
  const readable = new ReadableStream<Uint8Array>({
    start(controller) {
      push = (text) => controller.enqueue(new TextEncoder().encode(text));
      end = () => controller.close();
    },
  });
  const written: string[] = [];
  const writable = new WritableStream<Uint8Array>({ write: (chunk) => void written.push(new TextDecoder().decode(chunk)) });
  const port: SerialPortLike & { open: ReturnType<typeof vi.fn>; close: ReturnType<typeof vi.fn> } = {
    open: vi.fn().mockResolvedValue(undefined),
    close: vi.fn().mockResolvedValue(undefined),
    readable,
    writable,
  };
  return { port, push, end, written };
}

function setup(over: Partial<{ port: SerialPortLike }> = {}) {
  const fake = fakePort();
  const readings: ScaleReading[] = [];
  const statuses: { status: ScaleStatus; message?: string }[] = [];
  const scale = new SerialScale({
    port: over.port ?? fake.port,
    protocol: 'cas',
    baudRate: 9600,
    onReading: (r) => readings.push(r),
    onStatus: (status, message) => statuses.push({ status, message }),
  });
  return { ...fake, scale, readings, statuses };
}

const tick = () => new Promise((r) => setTimeout(r, 10));

describe('takeLines', () => {
  it('keeps an unfinished line for the next chunk', () => {
    const first = takeLines('', 'ST,GS,  1.2');
    expect(first).toEqual({ lines: [], rest: 'ST,GS,  1.2' });
    expect(takeLines(first.rest, '50kg\r\nST,GS,  1.2')).toEqual({ lines: ['ST,GS,  1.250kg'], rest: 'ST,GS,  1.2' });
  });

  it('drops blank lines', () => {
    expect(takeLines('', '\r\n\r\nA\r\n').lines).toEqual(['A']);
  });
});

describe('SerialScale', () => {
  it('opens the port at the chosen speed and reports connected', async () => {
    const t = setup();
    await t.scale.connect();
    expect(t.port.open).toHaveBeenCalledWith({ baudRate: 9600 });
    expect(t.statuses.map((s) => s.status)).toEqual(['connecting', 'connected']);
    await t.scale.disconnect();
  });

  it('turns each streamed line into a reading, even when a line arrives in pieces', async () => {
    const t = setup();
    await t.scale.connect();
    t.push('ST,GS,  1.2');
    t.push('50kg\r\nUS,GS,  0.100kg\r\n');
    await tick();
    expect(t.readings.map((r) => [r.weight, r.isStable])).toEqual([[1.25, true], [0.1, false]]);
    await t.scale.disconnect();
  });

  it('sends the zero and tare commands', async () => {
    const t = setup();
    await t.scale.connect();
    await t.scale.zero();
    await t.scale.tare();
    expect(t.written).toEqual(['Z\r\n', 'T\r\n']);
    await t.scale.disconnect();
  });

  it('reports a port that will not open', async () => {
    const t = setup();
    t.port.open.mockRejectedValue(new Error('Port busy'));
    await t.scale.connect();
    expect(t.statuses.at(-1)).toEqual({ status: 'error', message: 'Port busy' });
  });

  it('reports the scale being unplugged as disconnected', async () => {
    const t = setup();
    await t.scale.connect();
    t.end();
    await tick();
    expect(t.statuses.at(-1)?.status).toBe('disconnected');
  });

  it('closes the port and says disconnected when told to stop', async () => {
    const t = setup();
    await t.scale.connect();
    await t.scale.disconnect();
    expect(t.port.close).toHaveBeenCalled();
    expect(t.statuses.at(-1)?.status).toBe('disconnected');
  });
});
