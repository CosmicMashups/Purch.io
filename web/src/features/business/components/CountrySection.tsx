import { useState } from 'react';
import { toast } from '../../../components/feedback/toastStore';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { useUpdateCountry } from '../../tenant/queries';
import type { TenantSettings } from '../../tenant/types';

/** Mirrors Purch.Domain.CountryProfiles. Add a row here and there to support another country. */
const COUNTRIES = [{ code: 'PH', name: 'Philippines', currency: 'PHP', vatRate: 12 }];

export function CountrySection({ settings }: { settings: TenantSettings }) {
  const save = useUpdateCountry();
  const [country, setCountry] = useState(settings.country ?? 'PH');
  const profile = COUNTRIES.find((c) => c.code === country) ?? COUNTRIES[0];

  return (
    <section aria-labelledby="country-heading" className="rounded-panel border border-line bg-surface p-6">
      <h2 id="country-heading" className="text-xl font-bold">
        Country
      </h2>
      <p className="mt-1 text-base text-ink-soft">Sets the currency and the VAT rate used on purchase orders.</p>
      <form
        className="mt-5 flex flex-col gap-5"
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate(country, { onSuccess: () => toast.success('Country saved') });
        }}
      >
        <FormField label="Country">
          <select value={country} onChange={(e) => setCountry(e.target.value)} className={controlClass}>
            {COUNTRIES.map((c) => (
              <option key={c.code} value={c.code}>
                {c.name}
              </option>
            ))}
          </select>
        </FormField>
        <p className="text-base text-ink-soft">
          Currency {profile.currency}, VAT {profile.vatRate}%.
        </p>
        <div>
          <PrimaryButton type="submit" busy={save.isPending}>
            Save country
          </PrimaryButton>
        </div>
      </form>
    </section>
  );
}
