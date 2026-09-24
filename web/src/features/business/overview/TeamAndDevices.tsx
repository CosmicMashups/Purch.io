import { useState } from 'react';
import { Waffle } from '../../../components/charts/Proportions';
import { TimelineDots } from '../../../components/charts/TimelineDots';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { deviceTypeLabels } from '../types';
import { useDevices } from '../deviceQueries';
import { useStaff } from '../staffQueries';
import { teamItems } from './team';

/** One square per person, grouped by role. Small teams read better as individuals than as a bar. */
export function TeamPanel() {
  const staff = useStaff();
  return (
    <AsyncPanel
      title="Team"
      subtitle="One square per person. Outlined squares are switched off."
      query={staff}
      isEmpty={(d) => d.length === 0}
      emptyMessage="No staff yet."
    >
      {(d) => <Waffle items={teamItems(d)} unit="staff" ariaLabel="Staff by role" />}
    </AsyncPanel>
  );
}

/** When each paired device last checked in. A device gone quiet for days is worth a phone call. */
export function DevicesPanel() {
  const devices = useDevices();
  const [now] = useState(() => Date.now());
  return (
    <AsyncPanel
      title="Devices"
      subtitle="When each one last checked in, over the past week"
      query={devices}
      isEmpty={(d) => d.length === 0}
      emptyMessage="No devices are paired yet."
    >
      {(d) => (
        <TimelineDots
          now={now}
          rows={[...d]
            .sort((a, b) => (b.lastSeenAt ?? '').localeCompare(a.lastSeenAt ?? ''))
            .map((device) => ({ key: device.id, label: device.deviceIdentifier ?? deviceTypeLabels[device.deviceType] ?? 'Device', detail: deviceTypeLabels[device.deviceType], seenAt: device.lastSeenAt }))}
        />
      )}
    </AsyncPanel>
  );
}
