import { Link } from 'react-router-dom';
import { primaryButton } from './KioskActionBar';
import { KioskSlipSettings } from './KioskSlipSettings';

/** Staff setup for the kiosk's printer, reached by holding the logo. Customers have no way here. */
export function KioskPrinterPage() {
  return (
    <main className="kiosk-scroll h-full overflow-y-auto p-6">
      <div className="mx-auto flex max-w-2xl flex-col gap-6">
        <div>
          <h1 className="text-4xl font-extrabold tracking-tight">Printer setup</h1>
          <p className="text-xl text-ink-soft">For staff. These settings belong to this kiosk's browser.</p>
        </div>
        <section className="rounded-panel border border-line bg-surface p-6">
          <KioskSlipSettings />
        </section>
        <Link to="/kiosk" className={`${primaryButton} self-start`}>
          Back to the kiosk
        </Link>
      </div>
    </main>
  );
}
