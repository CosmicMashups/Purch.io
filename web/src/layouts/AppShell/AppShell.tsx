import { Link, Outlet, useLocation } from 'react-router-dom';
import { Cube, House, CashRegister, SignOut, Storefront, WifiSlash } from '@phosphor-icons/react';
import type { Icon } from '@phosphor-icons/react';
import { BrandMark } from '../../components/brand/Brand';
import { signOut } from '../../features/auth/signOut';
import { useSession } from '../../features/auth/useSession';
import { useOnlineStatus } from '../../hooks/useOnlineStatus';
import { tabPath, tabsForRole, type AppTab } from '../../permissions/navPolicy';

const TAB_META: Record<AppTab, { label: string; icon: Icon; matches: (path: string) => boolean }> = {
  home: { label: 'Home', icon: House, matches: (p) => p === '/' },
  sell: { label: 'Sell', icon: CashRegister, matches: (p) => p.startsWith('/sell') },
  inventory: { label: 'Inventory', icon: Cube, matches: (p) => p.startsWith('/inventory') },
  business: {
    label: 'Business',
    icon: Storefront,
    matches: (p) => p.startsWith('/business') || p.startsWith('/catalog'),
  },
};

function tabClass(active: boolean): string {
  return [
    'flex min-h-14 flex-1 flex-col items-center justify-center gap-1 rounded-control px-2 py-2 text-sm font-medium md:flex-none md:min-h-20 md:w-full',
    active ? 'bg-brand text-on-brand' : 'text-ink-soft hover:bg-canvas hover:text-ink',
  ].join(' ');
}

export function AppShell() {
  const { pathname } = useLocation();
  const { role } = useSession();
  const online = useOnlineStatus();
  const tabs = tabsForRole(role);

  return (
    <div className="min-h-dvh md:pl-28 print:pl-0">
      <nav
        aria-label="Main"
        className="fixed inset-x-0 bottom-0 z-40 print:hidden flex items-stretch gap-1 border-t border-line bg-surface p-2 md:inset-y-0 md:right-auto md:w-28 md:flex-col md:justify-start md:border-t-0 md:border-r"
      >
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
        <button
          type="button"
          onClick={() => void signOut()}
          className="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 rounded-control px-2 py-2 text-sm font-medium text-ink-soft hover:bg-canvas hover:text-ink md:mt-auto md:min-h-20 md:flex-none"
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

      <main className="mx-auto max-w-6xl px-4 pb-28 pt-6 md:pb-10">
        {role && <p className="mb-4 text-sm text-ink-soft print:hidden">Signed in as {role}</p>}
        <Outlet />
      </main>
    </div>
  );
}
