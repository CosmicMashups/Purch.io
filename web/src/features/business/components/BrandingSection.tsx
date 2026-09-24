import { useForm, useWatch } from 'react-hook-form';
import { SAMPLE_IMAGE } from '../../../lib/images';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { ColorField } from '../../../components/forms/ColorField';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { ImageUploadField } from '../../../components/forms/ImageUploadField';
import { readableOn, validHex } from '../../../theme/branding';
import { useUpdateBranding } from '../../tenant/queries';
import type { TenantSettings } from '../../tenant/types';
import { brandingSchema, brandingWarnings, toBrandingBody, type BrandingForm } from '../settingsRules';

export function BrandingSection({ settings }: { settings: TenantSettings }) {
  const save = useUpdateBranding();
  const {
    register,
    control,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<BrandingForm>({
    resolver: zodResolver(brandingSchema),
    defaultValues: {
      logoUrl: settings.brandingLogoUrl ?? '',
      kioskPosterImageUrl: settings.kioskPosterImageUrl ?? '',
      accentColorHex: settings.brandingAccentColorHex ?? '',
      backgroundColorHex: settings.brandingBackgroundColorHex ?? '',
      primaryTextColorHex: settings.brandingPrimaryTextColorHex ?? '',
      secondaryTextColorHex: settings.brandingSecondaryTextColorHex ?? '',
      fontFamily: settings.brandingFontFamily ?? '',
    },
  });
  const values = useWatch({ control });
  const warnings = brandingWarnings({
    backgroundColorHex: values.backgroundColorHex ?? '',
    primaryTextColorHex: values.primaryTextColorHex ?? '',
    secondaryTextColorHex: values.secondaryTextColorHex ?? '',
  });
  const accent = validHex(values.accentColorHex) ?? '#0f766e';

  const color = (name: 'accentColorHex' | 'backgroundColorHex' | 'primaryTextColorHex' | 'secondaryTextColorHex', label: string, hint?: string) => (
    <ColorField label={label} hint={hint} value={values[name] ?? ''} error={errors[name]?.message} onChange={(v) => setValue(name, v, { shouldDirty: true, shouldValidate: true })} />
  );

  const submit = handleSubmit((v) => {
    save.mutate(toBrandingBody(v), { onSuccess: () => toast.success('Look and feel saved') });
  });

  return (
    <section aria-labelledby="branding-heading" className="rounded-panel border border-line bg-surface p-6">
      <h2 id="branding-heading" className="text-xl font-bold">
        Look and feel
      </h2>
      <p className="mt-1 text-base text-ink-soft">Your logo and colours appear across the app. Leave a colour blank to keep the Purch.io default.</p>

      <form onSubmit={submit} noValidate className="mt-5 flex flex-col gap-5">
        <div className="grid gap-5 sm:grid-cols-2">
          {color('accentColorHex', 'Main colour', 'Buttons and highlights')}
          {color('backgroundColorHex', 'Background colour')}
          {color('primaryTextColorHex', 'Main text colour')}
          {color('secondaryTextColorHex', 'Secondary text colour')}
        </div>

        {warnings.map((warning) => (
          <p key={warning} role="status" className="rounded-control border border-warn bg-surface p-3 text-base font-medium text-warn">
            {warning}
          </p>
        ))}

        <div
          aria-label="Preview"
          className="flex flex-wrap items-center gap-4 rounded-panel border border-line p-4"
          style={{ background: validHex(values.backgroundColorHex) ?? undefined, color: validHex(values.primaryTextColorHex) ?? undefined }}
        >
          <span className="text-base font-semibold">Preview</span>
          <span className="rounded-control px-5 py-2 text-base font-semibold" style={{ background: accent, color: readableOn(accent) }}>
            Charge
          </span>
          <span className="text-sm" style={{ color: validHex(values.secondaryTextColorHex) ?? undefined }}>
            Secondary text
          </span>
        </div>

        <FormField label="Font" hint="A font installed on the device, like Poppins. Leave blank for the default." error={errors.fontFamily?.message}>
          <input {...register('fontFamily')} className={controlClass} />
        </FormField>

        <input type="hidden" {...register('logoUrl')} />
        <ImageUploadField label="Logo" value={values.logoUrl || null} onChange={(url) => setValue('logoUrl', url ?? '', { shouldDirty: true })} />

        <input type="hidden" {...register('kioskPosterImageUrl')} />
        <ImageUploadField label="Kiosk poster" samples={[{ label: 'Use default', value: SAMPLE_IMAGE.kioskPoster }]} value={values.kioskPosterImageUrl || null} onChange={(url) => setValue('kioskPosterImageUrl', url ?? '', { shouldDirty: true })} />

        <div>
          <PrimaryButton type="submit" busy={save.isPending}>
            {save.isPending ? 'Saving...' : 'Save look and feel'}
          </PrimaryButton>
        </div>
      </form>
    </section>
  );
}
