import { NavLink, Outlet } from 'react-router-dom';

const navItems = [
  { to: '/catalog/items', label: 'Items' },
  { to: '/catalog/categories', label: 'Categories' },
  { to: '/catalog/modifier-groups', label: 'Modifier Groups' },
];

export function Layout() {
  return (
    <div className="min-h-screen bg-gray-50">
      <header className="border-b border-gray-200 bg-white">
        <nav className="mx-auto flex max-w-5xl items-center gap-4 px-4 py-3">
          <span className="font-semibold text-gray-900">Purch.io Catalog</span>
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `text-sm font-medium ${isActive ? 'text-gray-900' : 'text-gray-500 hover:text-gray-700'}`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
