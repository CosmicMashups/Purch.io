import { signOut } from '../../auth/signOut';

/**
 * The POS ties every sale to a paired device and its branch, and only a staff PIN sign-in carries that.
 * An email admin sign-in has neither, so the API would refuse every sale.
 */
export function DeviceRequired() {
  return (
    <section className="max-w-xl rounded-panel border border-line bg-surface p-8">
      <h1 className="text-2xl font-bold tracking-tight">Sign in with a device to sell</h1>
      <p className="mt-2 text-base text-ink-soft">
        Sales are recorded against a paired device and its branch. Sign out, then choose Staff PIN and enter this device's code and your PIN.
      </p>
      <button type="button" onClick={() => void signOut()} className="mt-6 h-12 rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong">
        Sign out
      </button>
    </section>
  );
}
