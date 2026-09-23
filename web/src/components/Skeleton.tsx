/** A single pulsing placeholder bar — the building block for a loading skeleton. */
export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-gray-200 ${className}`} />;
}

/** A stand-in for a table's rows while it loads: [rows] placeholder rows of [columns] bars, matching
 * the row height/padding of the real `<tbody>` rows this replaces so the layout doesn't jump once
 * data arrives. */
export function SkeletonRows({ rows = 5, columns = 4 }: { rows?: number; columns?: number }) {
  return (
    <div className="divide-y divide-gray-100" aria-hidden="true">
      {Array.from({ length: rows }, (_, rowIndex) => (
        <div key={rowIndex} className="flex items-center gap-4 px-4 py-3">
          {Array.from({ length: columns }, (_, columnIndex) => (
            <Skeleton
              key={columnIndex}
              className={columnIndex === 0 ? 'h-4 flex-[2]' : 'h-4 flex-1'}
            />
          ))}
        </div>
      ))}
    </div>
  );
}

/** A stand-in for a simple list (Categories, Modifier Groups) while it loads. */
export function SkeletonList({ rows = 4 }: { rows?: number }) {
  return (
    <div className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white" aria-hidden="true">
      {Array.from({ length: rows }, (_, rowIndex) => (
        <div key={rowIndex} className="flex items-center justify-between px-4 py-3">
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-4 w-12" />
        </div>
      ))}
    </div>
  );
}
