import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client';
import './index.css';
import { App } from './App.tsx';
import { ErrorBoundary } from './components/ErrorBoundary.tsx';
import { Toaster } from './components/feedback/Toaster.tsx';
import { decodeClaims } from './lib/jwt';
import { useAuthStore } from './lib/authStore';
import { queryClient } from './lib/queryClient';
import { CACHE_MAX_AGE_MS, CACHE_SCHEMA_VERSION, shouldPersistQuery } from './offline/db/cachePolicy';
import { createDexiePersister } from './offline/db/persister';
import { ThemeProvider } from './theme/ThemeProvider.tsx';

const persister = createDexiePersister(() => decodeClaims(useAuthStore.getState().accessToken)?.tenantId ?? null);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <PersistQueryClientProvider
        client={queryClient}
        persistOptions={{ persister, maxAge: CACHE_MAX_AGE_MS, buster: CACHE_SCHEMA_VERSION, dehydrateOptions: { shouldDehydrateQuery: shouldPersistQuery } }}
      >
        <BrowserRouter>
          <ThemeProvider>
            <App />
            <Toaster />
          </ThemeProvider>
        </BrowserRouter>
      </PersistQueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
);
