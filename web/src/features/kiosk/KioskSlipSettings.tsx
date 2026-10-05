import { useState } from 'react';
import { FormField, controlClass } from '../../components/forms/FormField';
import { useHardwareConfig, type PaperWidth } from '../../hardware/config';
import { useSlipPrinter } from './useSlipPrinter';
import { testSlip } from './slipData';
import { useBusinessName } from './useBusinessName';

const button = 'h-12 rounded-control border border-line bg-surface px-4 text-base font-semibold hover:border-brand disabled:opacity-50';

/**
 * Turning on the kiosk's order slip, step by step: pick the paper, print a test slip, say it came out right, then switch it
 * on. A browser cannot tell whether a printer is attached or working, so "set up" means a person saw the test slip print.
 * Changing the paper takes the confirmation away again.
 */
export function KioskSlipSettings({ showPaperWidth = true }: { showPaperWidth?: boolean }) {
  const config = useHardwareConfig();
  const businessName = useBusinessName();
  const printer = useSlipPrinter();
  const [tested, setTested] = useState(false);
  const confirmed = config.kioskSlipConfirmedWidth === config.paperWidth;

  return (
    <div className="flex flex-col gap-4">
      <p className="text-base">
        The kiosk can print a small slip with the order number, items and how the customer will pay. It stays off until you have printed a test slip on this computer and confirmed it came out right.
      </p>

      {showPaperWidth ? (
        <FormField label="Paper width">
          <select className={controlClass} value={config.paperWidth} onChange={(e) => config.update({ paperWidth: e.target.value as PaperWidth })}>
            <option value="mm80">80 mm</option>
            <option value="mm58">58 mm</option>
          </select>
        </FormField>
      ) : (
        <p className="text-sm text-ink-soft">The slip uses the paper width chosen under Receipt printing above. Changing it switches the slip off until it is tested again.</p>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          className={button}
          onClick={() => {
            printer.print(testSlip(businessName));
            setTested(true);
          }}
        >
          Print test slip
        </button>
        {(tested || confirmed) && (
          <button type="button" className={button} disabled={confirmed} onClick={() => config.update({ kioskSlipConfirmedWidth: config.paperWidth })}>
            {confirmed ? 'Confirmed for this paper' : 'It printed correctly'}
          </button>
        )}
      </div>
      {!confirmed && tested && <p className="text-sm text-ink-soft">If nothing came out or it looked wrong, check the printer and paper and print the test slip again.</p>}

      <label className="flex min-h-12 items-center justify-between gap-3 text-base font-semibold">
        <span>
          Print an order slip when an order is sent
          <span className="block text-sm font-normal text-ink-soft">
            {confirmed ? 'Chrome and Edge skip the print window when started with the kiosk printing option. Otherwise the customer would have to confirm each print.' : 'Available after a test slip is confirmed.'}
          </span>
        </span>
        <input
          type="checkbox"
          role="switch"
          disabled={!confirmed}
          checked={config.kioskPrintSlip && confirmed}
          onChange={(e) => config.update({ kioskPrintSlip: e.target.checked })}
          className="size-7 shrink-0 accent-brand disabled:opacity-40"
        />
      </label>
      {printer.portal}
    </div>
  );
}
