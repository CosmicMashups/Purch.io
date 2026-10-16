import { useState } from 'react';
import { controlClass, SecondaryButton } from '../../../components/forms/FormField';
import { DecisionReason, PlannedCreateKind, type ImportBatch, type ImportDecision, type ImportResolution } from '../types';

const createKind = (k: number) => (k === PlannedCreateKind.Category ? 'Category' : k === PlannedCreateKind.Item ? 'Item' : 'Ingredient');
const reasonText = (r: number) =>
  r === DecisionReason.SimilarCategory ? 'Looks like an existing category' : r === DecisionReason.SimilarName ? 'Looks like an existing record' : 'No SKU or barcode to match on';
const lineText = (lines: number[]) => `line${lines.length === 1 ? '' : 's'} ${lines.join(', ')}`;

function DecisionCard({ decision, busy, onSave }: { decision: ImportDecision; busy: boolean; onSave: (key: string, r: ImportResolution) => void }) {
  const first = decision.nearest[0];
  const [choice, setChoice] = useState<ImportResolution['choice']>(first ? 'link' : 'create');
  const [targetId, setTargetId] = useState(first?.id ?? '');
  const [newName, setNewName] = useState(decision.name);
  const valid = choice === 'create' || (choice === 'link' && !!targetId) || (choice === 'rename' && newName.trim().length > 0);
  const options: { id: ImportResolution['choice']; label: string; hidden?: boolean }[] = [
    { id: 'link', label: 'Link to existing', hidden: decision.nearest.length === 0 },
    { id: 'create', label: 'Create new' },
    { id: 'rename', label: 'Rename' },
  ];

  return (
    <li className="flex flex-col gap-3 rounded-panel border border-line bg-surface p-4">
      <div>
        <p className="text-base font-semibold text-ink">{decision.name}</p>
        <p className="text-sm text-ink-soft">{reasonText(decision.reason)} ({lineText(decision.lines)})</p>
      </div>
      {decision.nearest.length > 0 && <p className="text-sm text-ink-soft">Existing: {decision.nearest.map((n) => n.name).join(', ')}</p>}
      <div role="radiogroup" aria-label={`Decision for ${decision.name}`} className="flex flex-wrap gap-2">
        {options
          .filter((o) => !o.hidden)
          .map((o) => (
            <button
              key={o.id}
              type="button"
              role="radio"
              aria-checked={choice === o.id}
              onClick={() => setChoice(o.id)}
              className={`h-12 rounded-control border px-4 text-base font-semibold ${choice === o.id ? 'border-brand bg-brand-tint text-brand-strong' : 'border-ink-soft/40 text-ink'}`}
            >
              {o.label}
            </button>
          ))}
      </div>
      {choice === 'link' && (
        <select aria-label="Existing record" value={targetId} onChange={(e) => setTargetId(e.target.value)} className={controlClass}>
          {decision.nearest.map((n) => (
            <option key={n.id} value={n.id}>
              {n.name}
            </option>
          ))}
        </select>
      )}
      {choice === 'rename' && <input aria-label="New name" value={newName} onChange={(e) => setNewName(e.target.value)} className={controlClass} />}
      <div>
        <SecondaryButton
          type="button"
          disabled={!valid || busy}
          onClick={() => onSave(decision.key, { choice, targetId: choice === 'link' ? targetId : null, newName: choice === 'rename' ? newName.trim() : null })}
        >
          Save choice
        </SecondaryButton>
      </div>
    </li>
  );
}

interface Props {
  batch: ImportBatch;
  confirmed: boolean[];
  onConfirmedChange: (next: boolean[]) => void;
  busy: boolean;
  onResolve: (key: string, resolution: ImportResolution) => void;
}

/** The review groups plus row errors, auto-links and the unchanged count. Cost columns show only when present. */
export function ImportReview({ batch, confirmed, onConfirmedChange, busy, onResolve }: Props) {
  const { plan } = batch;
  const allTicked = confirmed.length > 0 && confirmed.every(Boolean);
  // An auto-link that came from a saved Link resolution can be undone by turning that resolution into Create.
  const undoKey = (existingId: string) =>
    Object.entries(batch.resolutions).find(([k, r]) => k.startsWith('cat:') && r.choice === 'link' && r.targetId === existingId)?.[0];

  return (
    <div className="flex flex-col gap-6">
      {plan.rowErrors.length > 0 && (
        <section aria-labelledby="imp-errors" className="flex flex-col gap-2">
          <h2 id="imp-errors" className="text-lg font-semibold text-red-900">
            Fix these rows in the file ({plan.rowErrors.length})
          </h2>
          <ul className="flex flex-col gap-2">
            {plan.rowErrors.map((e) => (
              <li key={`${e.line}-${e.message}`} className="rounded-panel border border-red-300 bg-red-50 p-3 text-base text-red-900">
                Line {e.line}: {e.message}
              </li>
            ))}
          </ul>
        </section>
      )}

      {plan.needsDecision.length > 0 && (
        <section aria-labelledby="imp-decide" className="flex flex-col gap-2">
          <h2 id="imp-decide" className="text-lg font-semibold text-ink">
            Needs a decision ({plan.needsDecision.length})
          </h2>
          <ul className="flex flex-col gap-3">
            {plan.needsDecision.map((d) => (
              <DecisionCard key={d.key} decision={d} busy={busy} onSave={onResolve} />
            ))}
          </ul>
        </section>
      )}

      {plan.willCreate.length > 0 && (
        <section aria-labelledby="imp-create" className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="imp-create" className="text-lg font-semibold text-ink">
              Will create ({plan.willCreate.length})
            </h2>
            <label className="flex min-h-12 items-center gap-2 text-base font-semibold text-ink">
              <input type="checkbox" className="size-6" checked={allTicked} onChange={(e) => onConfirmedChange(plan.willCreate.map(() => e.target.checked))} />
              Confirm all
            </label>
          </div>
          <ul className="flex flex-col gap-2">
            {plan.willCreate.map((c, i) => (
              <li key={`${c.key ?? c.name}-${i}`} className="rounded-panel border border-line bg-surface">
                <label className="flex min-h-12 items-center gap-3 p-3 text-base text-ink">
                  <input
                    type="checkbox"
                    className="size-6"
                    checked={!!confirmed[i]}
                    onChange={(e) => onConfirmedChange(plan.willCreate.map((_, j) => (j === i ? e.target.checked : !!confirmed[j])))}
                    aria-label={`Confirm ${createKind(c.kind)} ${c.name}`}
                  />
                  <span className="flex-1">
                    <span className="font-semibold">{c.name}</span>{' '}
                    <span className="text-sm text-ink-soft">
                      {createKind(c.kind)}, {lineText(c.lines)}
                      {c.openingStock != null ? `, opening stock ${c.openingStock}` : ''}
                    </span>
                  </span>
                </label>
              </li>
            ))}
          </ul>
        </section>
      )}

      {plan.willUpdate.length > 0 && (
        <section aria-labelledby="imp-update" className="flex flex-col gap-2">
          <h2 id="imp-update" className="text-lg font-semibold text-ink">
            Will update ({plan.willUpdate.length})
          </h2>
          <ul className="flex flex-col gap-2">
            {plan.willUpdate.map((u) => (
              <li key={`${u.line}-${u.existingId}`} className="rounded-panel border border-line bg-surface p-3 text-base text-ink">
                <p className="text-sm text-ink-soft">Line {u.line}</p>
                <ul>
                  {u.changes.map((c) => (
                    <li key={c.field}>
                      {c.field}: <span className="text-ink-soft">{c.before ?? 'empty'}</span> to <span className="font-semibold">{c.after ?? 'empty'}</span>
                    </li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </section>
      )}

      {plan.autoLinks.length > 0 && (
        <section aria-labelledby="imp-links" className="flex flex-col gap-2">
          <h2 id="imp-links" className="text-lg font-semibold text-ink">
            Matched to existing categories
          </h2>
          <ul className="flex flex-col gap-2">
            {plan.autoLinks.map((l) => {
              const key = undoKey(l.existingId);
              return (
                <li key={`${l.line}-${l.csvName}`} className="flex flex-wrap items-center justify-between gap-2 rounded-panel border border-line bg-surface p-3 text-base text-ink">
                  <span>
                    Line {l.line}: {l.csvName} is the existing {l.existingName}
                  </span>
                  {key && (
                    <SecondaryButton type="button" disabled={busy} onClick={() => onResolve(key, { choice: 'create' })}>
                      Undo
                    </SecondaryButton>
                  )}
                </li>
              );
            })}
          </ul>
        </section>
      )}

      {plan.unchangedCount > 0 && (
        <p className="text-base text-ink-soft">
          {plan.unchangedCount} row{plan.unchangedCount === 1 ? '' : 's'} unchanged.
        </p>
      )}
    </div>
  );
}
