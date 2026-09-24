import { describe, expect, it } from 'vitest';
import { ApiError, userMessage } from './apiError';

describe('userMessage', () => {
  it('passes business validation and conflict messages through', () => {
    expect(userMessage(new ApiError('validation', 'Price cannot be negative'))).toBe('Price cannot be negative');
    expect(userMessage(new ApiError('conflict', 'Barcode already in use'))).toBe('Barcode already in use');
  });

  it('never leaks raw server text for other failures', () => {
    const leaked = new ApiError('unknown', 'System.NullReferenceException at Purch.Api.Endpoints');
    expect(userMessage(leaked)).not.toContain('NullReference');
    expect(userMessage(new ApiError('network', 'ECONNREFUSED 10.0.0.1'))).toMatch(/Cannot reach the server/);
  });

  it('handles non-ApiError values', () => {
    expect(userMessage(new Error('boom'))).toBe('Something went wrong. Please try again.');
    expect(userMessage(undefined)).toBe('Something went wrong. Please try again.');
  });
});
