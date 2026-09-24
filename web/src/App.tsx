import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './layouts/AppShell/AppShell';
import { RequireAuth } from './features/auth/RequireAuth';
import { RequireTab } from './features/auth/RequireTab';
import { LoginPage } from './features/auth/LoginPage';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>

          <Route element={<RequireTab tab="sell" />}>
            <Route path="/sell/*" element={<Navigate to="/sell" replace />} />
          </Route>

          <Route element={<RequireTab tab="inventory" />}>
            <Route path="/inventory/*" element={<Navigate to="/inventory" replace />} />
          </Route>

          <Route element={<RequireTab tab="business" />}>
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
