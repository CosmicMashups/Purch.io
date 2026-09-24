import { describe, expect, it } from 'vitest';
import { STEP_FIELDS, bootstrapSchema, passwordProblem, toBootstrapBody, type BootstrapForm } from './rules';

const good: BootstrapForm = {
  tenantName: 'Kape Katipunan',
  businessType: 2,
  branchName: 'Main',
  adminName: 'Mario Cruz',
  adminPin: '4321',
  adminEmail: '',
  adminPassword: '',
  agreed: true,
};

const messages = (v: object) => {
  const r = bootstrapSchema.safeParse(v);
  return r.success ? [] : r.error.issues.map((i) => i.message);
};

describe('passwordProblem', () => {
  it('needs 8 to 128 characters', () => {
    expect(passwordProblem('12345678')).toBeNull();
    expect(passwordProblem('1234567')).toMatch(/8 to 128/);
    expect(passwordProblem('x'.repeat(129))).toMatch(/8 to 128/);
    expect(passwordProblem('  ')).toBe('Enter a password');
  });
});

describe('bootstrapSchema', () => {
  it('accepts a PIN-only owner with no email', () => {
    expect(bootstrapSchema.safeParse(good).success).toBe(true);
  });

  it('accepts an email with a password, and requires them together', () => {
    expect(bootstrapSchema.safeParse({ ...good, adminEmail: 'mario@kape.ph', adminPassword: 'longenough1' }).success).toBe(true);
    expect(messages({ ...good, adminEmail: 'mario@kape.ph' })).toContain('Add a password too, or clear the email');
    expect(messages({ ...good, adminPassword: 'longenough1' })).toContain('Add an email too, or clear the password');
  });

  it('checks the password length and email format', () => {
    expect(messages({ ...good, adminEmail: 'mario@kape.ph', adminPassword: 'short' })).toContain('Password must be 8 to 128 characters');
    expect(messages({ ...good, adminEmail: 'not-an-email', adminPassword: 'longenough1' })).toContain('Enter a valid email');
  });

  it('applies the PIN rule', () => {
    expect(messages({ ...good, adminPin: '12' })).toContain('Use 4 to 8 digits');
    expect(messages({ ...good, adminPin: '' })).toContain('Enter a PIN');
  });

  it('requires names and the agreement', () => {
    expect(messages({ ...good, tenantName: ' ', branchName: '', adminName: '' })).toEqual(
      expect.arrayContaining(['Enter your business name', 'Enter your first branch name', 'Enter your name']),
    );
    expect(messages({ ...good, agreed: false })).toContain('Please review and accept to continue');
  });
});

describe('STEP_FIELDS', () => {
  it('covers every field exactly once', () => {
    const all = STEP_FIELDS.flat();
    expect(new Set(all).size).toBe(all.length);
    expect([...all].sort()).toEqual(Object.keys(good).sort());
  });
});

describe('toBootstrapBody', () => {
  it('trims, and sends no email or password for a PIN-only owner', () => {
    expect(toBootstrapBody({ ...good, tenantName: ' Kape ', adminPin: ' 4321 ' })).toEqual({
      tenantName: 'Kape',
      businessType: 2,
      branchName: 'Main',
      adminName: 'Mario Cruz',
      adminPin: '4321',
      adminEmail: null,
      adminPassword: null,
    });
  });

  it('sends both email and password when given', () => {
    expect(toBootstrapBody({ ...good, adminEmail: ' mario@kape.ph ', adminPassword: 'longenough1' })).toMatchObject({ adminEmail: 'mario@kape.ph', adminPassword: 'longenough1' });
  });
});
