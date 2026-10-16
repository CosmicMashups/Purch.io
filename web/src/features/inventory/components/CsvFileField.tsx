import { FileArrowUp } from '@phosphor-icons/react';
import { useId } from 'react';

export const MAX_CSV_BYTES = 5 * 1024 * 1024;

/** Picks one .csv file; the page refuses anything over 5 MB before it is uploaded. */
export function CsvFileField({ file, onPick, error }: { file: File | null; onPick: (file: File | null) => void; error?: string | null }) {
  const id = useId();
  return (
    <div className="flex flex-col gap-2">
      <label htmlFor={id} className="flex min-h-12 cursor-pointer items-center gap-3 rounded-control border border-dashed border-ink-soft/40 bg-surface px-4 text-base text-ink">
        <FileArrowUp size={24} aria-hidden />
        <span className="font-semibold">{file ? file.name : 'Choose a CSV file'}</span>
        {file && <span className="text-sm text-ink-soft">{(file.size / 1024).toFixed(1)} KB</span>}
      </label>
      <input id={id} type="file" accept=".csv,text/csv" className="sr-only" onChange={(e) => {
          onPick(e.target.files?.[0] ?? null);
          // Clear so picking the same file again still fires a change.
          e.target.value = '';
        }} />
      {error && <p role="alert" className="text-sm text-danger">{error}</p>}
    </div>
  );
}
