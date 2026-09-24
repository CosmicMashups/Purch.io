import { parseScaleLine, type ScaleProtocol, type ScaleReading } from './parser';

/** The parts of the Web Serial API this uses, so it can be tested without a real device. */
export interface SerialPortLike {
  open(options: { baudRate: number }): Promise<void>;
  close(): Promise<void>;
  readable: ReadableStream<Uint8Array> | null;
  writable: WritableStream<Uint8Array> | null;
}

export interface SerialApi {
  requestPort(): Promise<SerialPortLike>;
}

export type ScaleStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

export interface SerialScaleOptions {
  port: SerialPortLike;
  protocol: ScaleProtocol;
  baudRate: number;
  onReading: (reading: ScaleReading) => void;
  onStatus: (status: ScaleStatus, message?: string) => void;
}

/** Web Serial is Chromium only, and needs a secure page (https or localhost). */
export function serialApi(): SerialApi | null {
  const api = (navigator as unknown as { serial?: SerialApi }).serial;
  return api ?? null;
}

/** Splits streamed text into complete lines and keeps the unfinished tail for the next chunk. */
export function takeLines(buffer: string, chunk: string): { lines: string[]; rest: string } {
  const parts = (buffer + chunk).split(/[\r\n]+/);
  const rest = parts.pop() ?? '';
  return { lines: parts.map((l) => l.trim()).filter(Boolean), rest };
}

export class SerialScale {
  private reader: ReadableStreamDefaultReader<Uint8Array> | null = null;
  private stopping = false;
  private readonly opts: SerialScaleOptions;

  constructor(opts: SerialScaleOptions) {
    this.opts = opts;
  }

  async connect(): Promise<void> {
    const { port, baudRate, onStatus } = this.opts;
    onStatus('connecting');
    try {
      await port.open({ baudRate });
    } catch (error) {
      onStatus('error', error instanceof Error ? error.message : 'The port could not be opened');
      return;
    }
    if (!port.readable) {
      onStatus('error', 'The port cannot be read');
      return;
    }
    this.stopping = false;
    onStatus('connected');
    void this.readLoop(port.readable);
  }

  private async readLoop(readable: ReadableStream<Uint8Array>): Promise<void> {
    const { protocol, onReading, onStatus } = this.opts;
    const decoder = new TextDecoder();
    let buffer = '';
    this.reader = readable.getReader();
    try {
      for (;;) {
        const { value, done } = await this.reader.read();
        if (done) break;
        const { lines, rest } = takeLines(buffer, decoder.decode(value, { stream: true }));
        buffer = rest;
        for (const line of lines) {
          const reading = parseScaleLine(protocol, line);
          if (reading) onReading(reading);
        }
      }
      if (!this.stopping) onStatus('disconnected');
    } catch (error) {
      if (!this.stopping) onStatus('error', error instanceof Error ? error.message : 'The scale stopped responding');
    } finally {
      this.reader?.releaseLock();
      this.reader = null;
    }
  }

  private async send(command: string): Promise<void> {
    const writable = this.opts.port.writable;
    if (!writable) return;
    const writer = writable.getWriter();
    try {
      await writer.write(new TextEncoder().encode(command));
    } finally {
      writer.releaseLock();
    }
  }

  zero(): Promise<void> {
    return this.send('Z\r\n');
  }

  tare(): Promise<void> {
    return this.send('T\r\n');
  }

  async disconnect(): Promise<void> {
    this.stopping = true;
    try {
      await this.reader?.cancel();
    } catch {
      // The stream may already be closed.
    }
    try {
      await this.opts.port.close();
    } catch {
      // Already closed, or the device was unplugged.
    }
    this.opts.onStatus('disconnected');
  }
}
