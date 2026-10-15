import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { downloadTextFile } from '../../../lib/download';
import { inventoryApi } from '../api';
import { useSession } from '../../auth/useSession';
import { ExportCsvButton } from './ExportCsvButton';

vi.mock('../api', () => ({ inventoryApi: { exportCsv: vi.fn() } }));
vi.mock('../../../lib/download', async (orig) => ({ ...(await orig<typeof import('../../../lib/download')>()), downloadTextFile: vi.fn() }));

vi.mock('../../auth/useSession', () => ({ useSession: vi.fn() }));

describe('ExportCsvButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useSession).mockReturnValue({ claims: null, role: 'Manager' });
  });

  it('is hidden from a cashier', () => {
    vi.mocked(useSession).mockReturnValue({ claims: null, role: 'Cashier' });
    render(<ExportCsvButton kind="items" label="Export items" />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('downloads the export under a dated filename', async () => {
    vi.mocked(inventoryApi.exportCsv).mockResolvedValue('type,sku\n');
    render(<ExportCsvButton kind="items" label="Export items" />);
    fireEvent.click(screen.getByRole('button', { name: 'Export items' }));
    await waitFor(() => expect(downloadTextFile).toHaveBeenCalledTimes(1));
    expect(inventoryApi.exportCsv).toHaveBeenCalledWith('items');
    expect(vi.mocked(downloadTextFile).mock.calls[0][0]).toMatch(/^items-\d{4}-\d{2}-\d{2}\.csv$/);
    expect(vi.mocked(downloadTextFile).mock.calls[0][1]).toBe('type,sku\n');
  });
});
