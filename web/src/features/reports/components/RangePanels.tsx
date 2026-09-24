import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { RankedList } from '../../dashboard/components/RankedList';
import { formatPeso } from '../../dashboard/format';
import { movementLabel } from '../../inventory/movement';
import { useDepartmentSales, useMovementSummary, useStaffPerformance } from '../queries';
import type { RangeParams } from '../types';

/** Every figure is the server's. Bars only scale each row against the largest in the same list. */
function shareOf(value: number, max: number): number {
  return max > 0 ? Math.max(0, value) / max : 0;
}

export function StaffPanels({ params }: { params: RangeParams }) {
  const report = useStaffPerformance(params);
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <AsyncPanel
        title="Sales per cashier"
        subtitle="Revenue rung up in this period"
        query={report}
        isEmpty={(d) => d.sales.length === 0}
        emptyMessage="No staff sales in this period."
      >
        {(d) => {
          const sorted = [...d.sales].sort((a, b) => b.totalSales - a.totalSales);
          const max = sorted[0]?.totalSales ?? 0;
          return (
            <RankedList
              rows={sorted.map((s) => ({
                key: s.staffUserId,
                label: s.staffName,
                share: shareOf(s.totalSales, max),
                detail: `${formatPeso(s.totalSales)}, ${s.transactionCount} sale${s.transactionCount === 1 ? '' : 's'}`,
              }))}
            />
          );
        }}
      </AsyncPanel>

      <AsyncPanel
        title="Shift attendance"
        subtitle="Shifts opened, and how many closed with a cash variance"
        query={report}
        isEmpty={(d) => d.shiftAttendance.length === 0}
        emptyMessage="No shifts were opened in this period."
      >
        {(d) => {
          const sorted = [...d.shiftAttendance].sort((a, b) => b.shiftsOpened - a.shiftsOpened);
          const max = sorted[0]?.shiftsOpened ?? 0;
          return (
            <RankedList
              rows={sorted.map((s) => ({
                key: s.staffUserId,
                label: s.staffName,
                share: shareOf(s.shiftsOpened, max),
                detail: `${s.shiftsOpened} opened, ${s.shiftsWithVariance} with variance`,
                tone: s.shiftsWithVariance > 0 ? 'warn' : 'brand',
              }))}
            />
          );
        }}
      </AsyncPanel>
    </div>
  );
}

export function DepartmentPanel({ params }: { params: RangeParams }) {
  const report = useDepartmentSales(params);
  return (
    <AsyncPanel
      title="Sales by department"
      subtitle="Revenue split by department. Items with no department are under General."
      query={report}
      isEmpty={(d) => d.length === 0}
      emptyMessage="No sales in this period."
    >
      {(d) => {
        const sorted = [...d].sort((a, b) => b.revenue - a.revenue);
        const max = sorted[0]?.revenue ?? 0;
        return (
          <RankedList
            rows={sorted.map((row) => ({
              key: row.departmentId ?? 'general',
              label: row.departmentName,
              share: shareOf(row.revenue, max),
              detail: formatPeso(row.revenue),
            }))}
          />
        );
      }}
    </AsyncPanel>
  );
}

export function MovementPanel({ params }: { params: RangeParams }) {
  const report = useMovementSummary(params);
  return (
    <AsyncPanel
      title="Stock movement"
      subtitle="Quantity moved by reason in this period"
      query={report}
      isEmpty={(d) => d.byType.length === 0}
      emptyMessage="No stock movements in this period."
    >
      {(d) => {
        const sorted = [...d.byType].sort((a, b) => b.totalQuantity - a.totalQuantity);
        const max = sorted[0]?.totalQuantity ?? 0;
        return (
          <RankedList
            rows={sorted.map((row) => ({
              key: String(row.type),
              label: movementLabel(row.type),
              share: shareOf(row.totalQuantity, max),
              detail: `${row.totalQuantity} across ${row.movementCount} record${row.movementCount === 1 ? '' : 's'}`,
            }))}
          />
        );
      }}
    </AsyncPanel>
  );
}
