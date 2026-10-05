import { useState } from 'react';
import { BrandMark } from '../../components/brand/Brand';
import { StaffMenuDialog } from './StaffMenuDialog';
import { useBusinessName } from './useBusinessName';
import { useLongPress } from './useLongPress';

/**
 * The business's logo with its name underneath, on the page itself with no bar or background behind it. The logo
 * doubles as the staff-only way out: hold it for three seconds to reset the device, which a customer will not do by
 * accident (the landing screen has the same hold on a corner of its own).
 */
export function KioskHeader() {
  const name = useBusinessName();
  const [resetting, setResetting] = useState(false);
  const hold = useLongPress(() => setResetting(true));

  return (
    <header className="flex shrink-0 flex-col items-center gap-1.5 px-6 pb-2 pt-4 landscape:flex-row landscape:justify-center landscape:gap-4 landscape:pb-1.5 landscape:pt-3">
      <span {...hold} className="select-none">
        <BrandMark size={56} />
      </span>
      {name && <p className="max-w-[28ch] truncate text-center text-xl font-extrabold tracking-tight landscape:text-2xl">{name}</p>}
      <StaffMenuDialog open={resetting} onClose={() => setResetting(false)} />
    </header>
  );
}
