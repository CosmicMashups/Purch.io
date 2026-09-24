import { Outlet } from 'react-router-dom';

/** The kiosk is portrait and customer-facing: one column, no staff navigation, large targets. */
export function KioskLayout() {
  return (
    <div className="min-h-dvh bg-canvas text-ink">
      <div className="mx-auto flex min-h-dvh max-w-xl flex-col">
        <Outlet />
      </div>
    </div>
  );
}
