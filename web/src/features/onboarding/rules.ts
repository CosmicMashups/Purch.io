import { z } from 'zod';
import { pinProblem } from '../business/staffRules';

export const PASSWORD_MIN = 8;
export const PASSWORD_MAX = 128;

/** Same rule as the API: 8 to 128 characters. */
export function passwordProblem(password: string): string | null {
  if (password.trim() === '') return 'Enter a password';
  if (password.length < PASSWORD_MIN || password.length > PASSWORD_MAX) return `Password must be ${PASSWORD_MIN} to ${PASSWORD_MAX} characters`;
  return null;
}

export const bootstrapSchema = z
  .object({
    tenantName: z.string().trim().min(1, 'Enter your business name'),
    businessType: z.number().int().min(0).max(8),
    branchName: z.string().trim().min(1, 'Enter your first branch name'),
    adminName: z.string().trim().min(1, 'Enter your name'),
    adminPin: z.string(),
    adminEmail: z.string().trim().min(1, 'Enter your email').email('Enter a valid email'),
    adminPassword: z.string(),
    agreed: z.boolean(),
  })
  .superRefine((v, ctx) => {
    const pin = pinProblem(v.adminPin);
    if (pin) ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['adminPin'], message: pin });

    const problem = passwordProblem(v.adminPassword);
    if (problem) ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['adminPassword'], message: problem });

    if (!v.agreed) ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['agreed'], message: 'Please review and accept to continue' });
  });

export type BootstrapForm = z.infer<typeof bootstrapSchema>;

/** The fields each step owns, so Next only checks what that step shows. */
export const STEP_FIELDS: readonly (readonly (keyof BootstrapForm)[])[] = [
  ['tenantName', 'businessType'],
  ['branchName'],
  ['adminName', 'adminPin', 'adminEmail', 'adminPassword', 'agreed'],
];

export interface BootstrapBody {
  tenantName: string;
  businessType: number;
  branchName: string;
  adminName: string;
  adminPin: string;
  adminEmail: string;
  adminPassword: string;
}

export function toBootstrapBody(v: BootstrapForm): BootstrapBody {
  return {
    tenantName: v.tenantName.trim(),
    businessType: v.businessType,
    branchName: v.branchName.trim(),
    adminName: v.adminName.trim(),
    adminPin: v.adminPin.trim(),
    adminEmail: v.adminEmail.trim(),
    adminPassword: v.adminPassword,
  };
}
