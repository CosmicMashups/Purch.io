import { useState } from 'react';
import { PageHeader } from '../components/PageHeader';
import { FormField, controlClass } from '../components/forms/FormField';
import { BAUD_RATES, useHardwareConfig, type PaperWidth } from './config';
import { CUSTOMER_DISPLAY_PATH, customerDisplaySupported } from './display/channel';
import { CameraScanDialog } from './scanner/CameraScanDialog';
import { cameraScanSupported } from './scanner/cameraSupport';
import { ScalePanel } from './scale/ScalePanel';
import type { ScaleProtocol } from './scale/parser';
import { serialApi } from './scale/serialScale';
import { useScale } from './scale/scaleStore';

const button = 'h-12 rounded-control border border-line bg-surface px-4 text-base font-semibold hover:border-brand disabled:opacity-50';

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-3 rounded-panel border border-line bg-surface p-5">
      <h2 className="text-xl font-bold">{title}</h2>
      {children}
    </section>
  );
}

const STATUS_TEXT = { disconnected: 'Not connected', connecting: 'Connecting…', connected: 'Connected', error: 'Problem' } as const;

export function HardwarePage() {
  const config = useHardwareConfig();
  const status = useScale((s) => s.status);
  const message = useScale((s) => s.message);
  const connect = useScale((s) => s.connect);
  const disconnect = useScale((s) => s.disconnect);
  const [cameraOpen, setCameraOpen] = useState(false);
  const [lastScan, setLastScan] = useState<string | null>(null);
  const canSerial = serialApi() !== null;

  return (
    <div className="flex max-w-3xl flex-col gap-5">
      <PageHeader title="Hardware" subtitle="These settings belong to this browser on this computer. They are not shared with other devices." backTo={{ to: '/sell', label: 'Cashier' }} />

      <Section title="Scale">
        {!canSerial && <p className="text-base text-ink-soft">Connecting a scale needs Chrome or Edge on a secure page. Weights can still be typed in.</p>}
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Scale type">
            <select className={controlClass} value={config.scaleProtocol} onChange={(e) => config.update({ scaleProtocol: e.target.value as ScaleProtocol })} disabled={status === 'connected'}>
              <option value="cas">CAS (AP-1, ER Plus, SW-1, PD-II)</option>
              <option value="mettlerToledo">Mettler-Toledo / Toledo 8217</option>
            </select>
          </FormField>
          <FormField label="Speed (baud)">
            <select className={controlClass} value={config.scaleBaudRate} onChange={(e) => config.update({ scaleBaudRate: Number(e.target.value) })} disabled={status === 'connected'}>
              {BAUD_RATES.map((rate) => (
                <option key={rate} value={rate}>
                  {rate}
                </option>
              ))}
            </select>
          </FormField>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          {status === 'connected' ? (
            <button type="button" className={button} onClick={() => void disconnect()}>
              Disconnect scale
            </button>
          ) : (
            <button type="button" className="h-12 rounded-control bg-brand px-5 text-base font-bold text-on-brand disabled:opacity-50" disabled={!canSerial || status === 'connecting'} onClick={() => void connect()}>
              Connect scale
            </button>
          )}
          <span role="status" className="text-base font-semibold">
            {STATUS_TEXT[status]}
          </span>
        </div>
        {message && (
          <p role="alert" className="text-base font-medium text-danger">
            {message}
          </p>
        )}
        {status === 'connected' && <ScalePanel />}
      </Section>

      <Section title="Barcode scanner">
        <p className="text-base">A USB or Bluetooth scanner in keyboard mode works anywhere on the Cashier screen: scan and the item is added. Nothing needs to be connected here.</p>
        <div className="flex flex-wrap items-center gap-3">
          <button type="button" className={button} disabled={!cameraScanSupported()} onClick={() => setCameraOpen(true)}>
            Test camera scan
          </button>
          {!cameraScanSupported() && <span className="text-sm text-ink-soft">This browser cannot scan with a camera.</span>}
          {lastScan && <span className="text-base">Last scan: <strong className="tabular-nums">{lastScan}</strong></span>}
        </div>
      </Section>

      <Section title="Customer display">
        <p className="text-base">Open the display in its own window, then drag it to the customer-facing screen. It follows the sale on this till.</p>
        <p className="text-sm text-ink-soft">It has to stay in this same browser. A separate computer or tablet cannot show it.</p>
        <div>
          <button type="button" className={button} disabled={!customerDisplaySupported()} onClick={() => window.open(CUSTOMER_DISPLAY_PATH, 'purch-customer-display')}>
            Open customer display
          </button>
        </div>
      </Section>

      <Section title="Receipt printing">
        <p className="text-base">Receipts print through the browser to whichever printer the computer has set up. Choose the paper the receipt printer uses.</p>
        <FormField label="Paper width">
          <select className={controlClass} value={config.paperWidth} onChange={(e) => config.update({ paperWidth: e.target.value as PaperWidth })}>
            <option value="mm80">80 mm</option>
            <option value="mm58">58 mm</option>
          </select>
        </FormField>
        <p className="text-sm text-ink-soft">In the print window, turn off headers and footers and set margins to none for the neatest receipt.</p>
      </Section>

      <Section title="Cash drawer and direct printer commands">
        <p className="text-base">Not available in the browser. A cash drawer opens from a signal sent through the receipt printer, and browsers cannot send those commands to a printer. Open the drawer by hand when needed.</p>
      </Section>

      {cameraOpen && (
        <CameraScanDialog
          onDetect={(code) => {
            setLastScan(code);
            setCameraOpen(false);
          }}
          onClose={() => setCameraOpen(false)}
        />
      )}
    </div>
  );
}
