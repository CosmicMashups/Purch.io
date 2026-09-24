import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import { renderPage, signInAs } from '../../test/render';
import { tenantApi } from '../tenant/api';
import type { TenantSettings } from '../tenant/types';
import { SettingsPage } from './SettingsPage';

vi.mock('../tenant/api', () => ({
  tenantApi: {
    get: vi.fn(),
    updateBranding: vi.fn(),
    updateBir: vi.fn(),
    updateBarcode: vi.fn(),
    updateCreditLedger: vi.fn(),
    updateInventoryTracking: vi.fn(),
  },
}));
vi.mock('../uploads/api', () => ({ uploadsApi: { uploadImage: vi.fn() } }));

const settings: TenantSettings = {
  id: 't',
  name: 'Kape Katipunan',
  businessType: 2,
  brandingLogoUrl: null,
  brandingBackgroundColorHex: null,
  brandingAccentColorHex: '#0F766E',
  brandingPrimaryTextColorHex: null,
  brandingSecondaryTextColorHex: null,
  brandingFontFamily: null,
  requiresBarcodePerItem: false,
  tin: '123-456-789-000',
  registeredBusinessName: 'Kape Katipunan Inc.',
  registeredAddress: null,
  creditLedgerRetentionDays: null,
  creditLedgerEnabled: true,
  kioskPosterImageUrl: null,
  useSeparateInventoryTracking: false,
};

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Admin');
  vi.mocked(tenantApi.get).mockResolvedValue(settings);
});

describe('SettingsPage', () => {
  it('loads the saved values', async () => {
    renderPage(<SettingsPage />);
    expect(await screen.findByLabelText('TIN')).toHaveValue('123-456-789-000');
    expect(screen.getByLabelText('Main colour')).toHaveValue('#0F766E');
    expect(screen.getByRole('switch', { name: 'Customer credit (utang)' })).toBeChecked();
    expect(screen.getByRole('switch', { name: 'Require a barcode on every item' })).not.toBeChecked();
  });

  it('shows a retryable error rather than an empty form when settings fail to load', async () => {
    vi.mocked(tenantApi.get).mockRejectedValue(new Error('boom'));
    renderPage(<SettingsPage />);
    expect(await screen.findByText('Settings could not be loaded')).toBeInTheDocument();
    expect(screen.queryByLabelText('TIN')).not.toBeInTheDocument();
  });

  it('saves the look and feel, sending blanks as null', async () => {
    vi.mocked(tenantApi.updateBranding).mockResolvedValue(settings);
    renderPage(<SettingsPage />);
    fireEvent.change(await screen.findByLabelText('Main colour'), { target: { value: '#B91C1C' } });
    fireEvent.change(screen.getByLabelText('Font'), { target: { value: 'Poppins' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save look and feel' }));

    await waitFor(() => expect(tenantApi.updateBranding).toHaveBeenCalledTimes(1));
    expect(tenantApi.updateBranding).toHaveBeenCalledWith({
      logoUrl: null,
      kioskPosterImageUrl: null,
      accentColorHex: '#B91C1C',
      backgroundColorHex: null,
      primaryTextColorHex: null,
      secondaryTextColorHex: null,
      fontFamily: 'Poppins',
    });
  });

  it('refuses a colour the API would refuse and sends nothing', async () => {
    renderPage(<SettingsPage />);
    fireEvent.change(await screen.findByLabelText('Background colour'), { target: { value: 'red' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save look and feel' }));
    expect(await screen.findByText('Use a colour like #0F766E')).toBeInTheDocument();
    expect(tenantApi.updateBranding).not.toHaveBeenCalled();
  });

  it('warns before saving colours the app would ignore for poor readability', async () => {
    renderPage(<SettingsPage />);
    fireEvent.change(await screen.findByLabelText('Background colour'), { target: { value: '#FFFFFF' } });
    fireEvent.change(screen.getByLabelText('Main text colour'), { target: { value: '#EEEEEE' } });
    expect(await screen.findByText(/hard to read on that background/)).toBeInTheDocument();
  });

  it('saves business details and turns blanks into null', async () => {
    vi.mocked(tenantApi.updateBir).mockResolvedValue(settings);
    renderPage(<SettingsPage />);
    fireEvent.change(await screen.findByLabelText(/Keep customer credit records/), { target: { value: '365' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save business details' }));
    await waitFor(() => expect(tenantApi.updateBir).toHaveBeenCalledWith({
      tin: '123-456-789-000',
      registeredBusinessName: 'Kape Katipunan Inc.',
      registeredAddress: null,
      creditLedgerRetentionDays: 365,
    }));
  });

  it('rejects a zero retention period before calling the API', async () => {
    renderPage(<SettingsPage />);
    fireEvent.change(await screen.findByLabelText(/Keep customer credit records/), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save business details' }));
    expect(await screen.findByText('Must be at least 1 day')).toBeInTheDocument();
    expect(tenantApi.updateBir).not.toHaveBeenCalled();
  });

  it('flips the barcode and credit switches straight away', async () => {
    vi.mocked(tenantApi.updateBarcode).mockResolvedValue({ ...settings, requiresBarcodePerItem: true });
    vi.mocked(tenantApi.updateCreditLedger).mockResolvedValue({ ...settings, creditLedgerEnabled: false });
    renderPage(<SettingsPage />);
    fireEvent.click(await screen.findByRole('switch', { name: 'Require a barcode on every item' }));
    await waitFor(() => expect(tenantApi.updateBarcode).toHaveBeenCalledWith(true));
    fireEvent.click(screen.getByRole('switch', { name: 'Customer credit (utang)' }));
    await waitFor(() => expect(tenantApi.updateCreditLedger).toHaveBeenCalledWith(false));
  });

  it('asks before changing how ingredients are tracked, and backing out changes nothing', async () => {
    vi.mocked(tenantApi.updateInventoryTracking).mockResolvedValue({ ...settings, useSeparateInventoryTracking: true });
    renderPage(<SettingsPage />);
    fireEvent.click(await screen.findByRole('switch', { name: 'Track ingredients separately' }));

    const dialog = screen.getByRole('dialog', { name: 'Track ingredients separately?' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(tenantApi.updateInventoryTracking).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('switch', { name: 'Track ingredients separately' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Turn on' }));
    await waitFor(() => expect(tenantApi.updateInventoryTracking).toHaveBeenCalledWith(true));
  });

  it('shows the server message when a save is refused and keeps the switch as it was', async () => {
    vi.mocked(tenantApi.updateBarcode).mockRejectedValue(new ApiError('validation', 'Some items have no barcode yet.'));
    renderPage(<SettingsPage />);
    const toggle = await screen.findByRole('switch', { name: 'Require a barcode on every item' });
    fireEvent.click(toggle);
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('Some items have no barcode yet.'));
    expect(toggle).not.toBeChecked();
  });
});
