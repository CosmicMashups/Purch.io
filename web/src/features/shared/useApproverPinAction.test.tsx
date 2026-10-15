import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { useApproverPinAction } from './useApproverPinAction';

const pinError = (msg: string) =>
  new ApiError('validation', 'x', { approverPin: [msg] });

describe('useApproverPinAction', () => {
  it('first call prompts, wrong PIN keeps dialog with isError, success calls onDone and closes', async () => {
    const send = vi.fn(async (pin?: string) => {
      if (!pin) throw pinError('PIN needed');
      if (pin !== '1234') throw pinError('Wrong PIN');
      return 'ok';
    });
    const onDone = vi.fn();
    const { result } = renderHook(() => useApproverPinAction({ send, onDone }));

    await act(async () => { await result.current.run('Do it'); });
    expect(result.current.dialogProps).toMatchObject({ title: 'Do it', message: 'PIN needed', isError: false });
    expect(onDone).not.toHaveBeenCalled();

    await act(async () => { await result.current.dialogProps!.onSubmit('0000'); });
    expect(result.current.dialogProps).toMatchObject({ message: 'Wrong PIN', isError: true });

    await act(async () => { await result.current.dialogProps!.onSubmit('1234'); });
    expect(onDone).toHaveBeenCalledWith('ok');
    expect(result.current.dialogProps).toBeNull();
  });
});
