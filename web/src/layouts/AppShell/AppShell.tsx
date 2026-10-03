import { useEffect, useState } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Cube, House, CashRegister, List, LockSimple, SignOut, Storefront, WifiSlash, X } from '@phosphor-icons/react';
import type { Icon } from '@phosphor-icons/react';
import { BrandMark } from '../../components/brand/Brand';
import { signOut } from '../../features/auth/signOut';
import { useSession } from '../../features/auth/useSession';
import { useOnlineStatus } from '../../hooks/useOnlineStatus';
import { useAutoLock } from '../../hooks/useAutoLock';
import { IDLE_STATE } from '../../hardware/display/channel';
import { publishToCustomerDisplay } from '../../hardware/display/serverFeed';
import { readDeviceCredential } from '../../features/kiosk/deviceCredential';
import { tabPath, tabsForRole, type AppTab } from '../../permissions/navPolicy';

const TAB_META: Record<AppTab, { label: string; icon: Icon; matches: (path: string) => boolean }> = {
  home: { label: 'Home', icon: House, matches: (p) => p === '/' },
  sell: { label: 'Cashier', icon: CashRegister, matches: (p) => p.startsWith('/sell') },
  inventory: { label: 'Inventory', icon: Cube, matches: (p) => p.startsWith('/inventory') },
  business: {
    label: 'Business',
    icon: Storefront,
    matches: (p) => p.startsWith('/business') || p.startsWith('/catalog'),
  },
};

function tabClass(active: boolean): string {
  return [
    'flex min-h-14 flex-none flex-col items-center justify-center gap-1 rounded-control px-2 py-2 text-sm font-medium md:min-h-20 md:w-full',
    active ? 'bg-brand text-on-brand' : 'text-ink-soft hover:bg-canvas hover:text-ink',
  ].join(' ');
}

export function AppShell() {
  const { pathname } = useLocation();
  const { role, claims } = useSession();
  const navigate = useNavigate();
  const online = useOnlineStatus();
  const tabs = tabsForRole(role);
  const [menuOpen, setMenuOpen] = useState(false);

  // On a paired till or warehouse device the session belongs to whoever unlocked it, so it locks itself when left alone.
  const onPairedDevice = !!claims?.deviceId && !!readDeviceCredential();
  function lock() {
    // The next customer should not see the last order while the till waits for the next person.
    publishToCustomerDisplay(IDLE_STATE);
    void signOut().then(() => navigate('/unlock', { replace: true }));
  }
  useAutoLock(onPairedDevice, lock);

  // Phones keep the navigation in a drawer: it closes when a page is chosen or Escape is pressed.
  useEffect(() => setMenuOpen(false), [pathname]);
  useEffect(() => {
    if (!menuOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setMenuOpen(false);
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [menuOpen]);

  return (
    <div className="min-h-dvh md:pl-28 print:pl-0">
      <header className="sticky top-0 z-30 flex items-center gap-3 border-b border-line bg-surface px-4 py-2 md:hidden print:hidden">
        <button
          type="button"
          aria-label="Open menu"
          aria-expanded={menuOpen}
          aria-controls="main-nav"
          onClick={() => setMenuOpen(true)}
          className="grid size-12 place-items-center rounded-control border border-line hover:border-brand"
        >
          <List size={26} aria-hidden="true" />
        </button>
        <BrandMark size={40} />
      </header>

      {menuOpen && <button type="button" aria-label="Close menu" tabIndex={-1} onClick={() => setMenuOpen(false)} className="fixed inset-0 z-40 bg-ink/40 md:hidden" />}

      <nav
        id="main-nav"
        aria-label="Main"
        className={`fixed inset-y-0 left-0 z-50 flex w-64 flex-col items-stretch gap-1 border-r border-line bg-surface p-2 transition-transform md:z-40 md:w-28 md:translate-x-0 md:justify-start print:hidden ${menuOpen ? 'translate-x-0' : '-translate-x-full'}`}
      >
        <button type="button" aria-label="Close menu" onClick={() => setMenuOpen(false)} className="grid size-12 place-items-center self-end rounded-control hover:bg-canvas md:hidden">
          <X size={24} aria-hidden="true" />
        </button>
        <div className="hidden justify-center pb-3 pt-2 md:flex">
          <BrandMark size={56} />
        </div>
        {tabs.map((tab) => {
          const { label, icon: TabIcon, matches } = TAB_META[tab];
          const active = matches(pathname);
          return (
            <Link key={tab} to={tabPath(tab)} aria-current={active ? 'page' : undefined} className={tabClass(active)}>
              <TabIcon size={28} weight={active ? 'fill' : 'regular'} aria-hidden="true" />
              {label}
            </Link>
          );
        })}
        {onPairedDevice && (
          <button
            type="button"
            onClick={lock}
            className="mt-auto flex min-h-14 flex-none flex-col items-center justify-center gap-1 rounded-control bg-brand px-2 py-2 text-sm font-semibold text-on-brand hover:bg-brand-strong md:min-h-20"
          >
            <LockSimple size={28} aria-hidden="true" />
            Lock
          </button>
        )}
        <button
          type="button"
          onClick={() => void signOut()}
          className={`${onPairedDevice ? '' : 'mt-auto'} flex min-h-14 flex-none flex-col items-center justify-center gap-1 rounded-control px-2 py-2 text-sm font-medium text-ink-soft hover:bg-canvas hover:text-ink md:min-h-20`}
        >
          <SignOut size={28} aria-hidden="true" />
          Sign out
        </button>
      </nav>

      {!online && (
        <div
          role="status"
          className="sticky top-0 z-30 flex items-center gap-3 print:hidden bg-warn px-4 py-3 text-base font-medium text-white"
        >
          <WifiSlash size={24} aria-hidden="true" />
          You are offline. You can browse the catalog you already loaded, but sales and changes are unavailable until the connection returns.
        </div>
      )}

      <main className={pathname === '/sell' ? 'pb-10 pl-28 pr-4 pt-4 md:pt-6' : 'mx-auto max-w-6xl px-4 pb-10 pt-4 md:pt-6'}>
        {role && pathname !== '/sell' && <p className="mb-4 text-sm text-ink-soft print:hidden">Signed in as {role}</p>}
        <Outlet />
      </main>
    </div>
  );
}
