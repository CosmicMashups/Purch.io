import { useOnlineStatus } from '../../hooks/useOnlineStatus';

interface Props {
  /** `dataUpdatedAt` from the query: when the shown data was last fetched from the server. */
  updatedAt: number;
  what: string;
}

/** Offline, saved data is still shown. Say plainly that it may be out of date, and since when. */
export function StaleDataNotice({ updatedAt, what }: Props) {
  const online = useOnlineStatus();
  if (online || updatedAt <= 0) return null;
  const when = new Date(updatedAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
  return (
    <p role="status" className="rounded-md border border-warn/40 bg-warn/10 px-3 py-2 text-sm text-ink">
      Showing {what} saved on {when}. They may be out of date, and changes are unavailable until you are back online.
    </p>
  );
}
