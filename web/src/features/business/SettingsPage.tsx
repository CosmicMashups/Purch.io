import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { PageHeader } from '../../components/PageHeader';
import { userMessage } from '../../lib/apiError';
import { useTenantSettings } from '../tenant/queries';
import { BirSection } from './components/BirSection';
import { BrandingSection } from './components/BrandingSection';
import { OptionsSection } from './components/OptionsSection';

export function SettingsPage() {
  const settings = useTenantSettings();

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <PageHeader title="Business settings" backTo={{ to: '/business', label: 'Business' }} />
      {settings.isPending && (
        <div className="flex flex-col gap-4" aria-busy="true">
          <Skeleton className="h-64 w-full" />
          <Skeleton className="h-48 w-full" />
        </div>
      )}
      {settings.isError && <ErrorState title="Settings could not be loaded" message={userMessage(settings.error)} onRetry={() => void settings.refetch()} />}
      {settings.isSuccess && (
        <>
          <BrandingSection settings={settings.data} />
          <BirSection settings={settings.data} />
          <OptionsSection settings={settings.data} />
        </>
      )}
    </div>
  );
}
