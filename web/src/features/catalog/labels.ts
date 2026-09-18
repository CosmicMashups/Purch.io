import { PricingType, TingiMode } from './types';

export const pricingTypeLabels: Record<PricingType, string> = {
  [PricingType.Unit]: 'Unit',
  [PricingType.WeightVolume]: 'Weight / Volume',
  [PricingType.Bundle]: 'Bundle / Promo',
  [PricingType.Service]: 'Service / Appointment',
  [PricingType.Combo]: 'Combo / Meal',
  [PricingType.VariantMatrix]: 'Variant Matrix',
};

export const tingiModeLabels: Record<TingiMode, string> = {
  [TingiMode.None]: 'None',
  [TingiMode.Fixed]: 'Fixed sizes',
  [TingiMode.Increment]: 'Increment step',
};
