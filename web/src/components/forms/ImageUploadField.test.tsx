import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { uploadsApi } from '../../features/uploads/api';
import { ApiError } from '../../lib/apiError';
import { ImageUploadField } from './ImageUploadField';

vi.mock('../../features/uploads/api', () => ({ uploadsApi: { uploadImage: vi.fn() } }));

function pick(container: HTMLElement, file: File) {
  const input = container.querySelector('input[type=file]') as HTMLInputElement;
  fireEvent.change(input, { target: { files: [file] } });
}

describe('ImageUploadField', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uploads a valid image and reports the hosted URL', async () => {
    vi.mocked(uploadsApi.uploadImage).mockResolvedValue('https://cdn.example/latte.png');
    const onChange = vi.fn();
    const { container } = render(<ImageUploadField label="Image" value={null} onChange={onChange} />);
    pick(container, new File(['x'], 'latte.png', { type: 'image/png' }));
    await waitFor(() => expect(onChange).toHaveBeenCalledWith('https://cdn.example/latte.png'));
  });

  it('rejects an unsupported file without calling the API', async () => {
    const onChange = vi.fn();
    const { container } = render(<ImageUploadField label="Image" value={null} onChange={onChange} />);
    pick(container, new File(['x'], 'logo.svg', { type: 'image/svg+xml' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('JPG, PNG, WebP or GIF');
    expect(uploadsApi.uploadImage).not.toHaveBeenCalled();
    expect(onChange).not.toHaveBeenCalled();
  });

  it('shows a friendly message when the server refuses the upload', async () => {
    vi.mocked(uploadsApi.uploadImage).mockRejectedValue(new ApiError('unknown', 'System.IO.IOException at Supabase'));
    const { container } = render(<ImageUploadField label="Image" value={null} onChange={vi.fn()} />);
    pick(container, new File(['x'], 'a.jpg', { type: 'image/jpeg' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong');
  });

  it('lets staff replace or remove an existing image', () => {
    const onChange = vi.fn();
    render(<ImageUploadField label="Image" value="https://cdn.example/a.png" onChange={onChange} />);
    expect(screen.getByRole('button', { name: 'Change image' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Remove' }));
    expect(onChange).toHaveBeenCalledWith(null);
  });
});
