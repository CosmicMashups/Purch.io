import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AreaLineChart } from './AreaLineChart';
import { CalendarHeatmap } from './CalendarHeatmap';
import { Donut } from './Donut';
import { Meter } from './Meter';
import { StackedStrip, Waffle } from './Proportions';
import { BarList, BulletList, DotPlot, LollipopList } from './RankedCharts';
import { TimelineDots } from './TimelineDots';

const peso = (n: number) => `₱${n}`;
const days = ['2026-09-20', '2026-09-21', '2026-09-22', '2026-09-23', '2026-09-24'];
const points = days.map((d, i) => ({ key: d, label: `Sep ${20 + i}`, value: [100, 250, 0, 900, 400][i] }));

describe('AreaLineChart', () => {
  const chart = () => render(<AreaLineChart points={points} seriesName="Revenue" formatValue={peso} formatAxis={peso} ariaLabel="Daily revenue" />);

  it('labels the best day directly and gives the axis clean ticks', () => {
    chart();
    expect(screen.getByText('Best: ₱900')).toBeInTheDocument();
    expect(screen.getByText('₱1000')).toBeInTheDocument();
  });

  it('carries every value in a table one tap away', () => {
    chart();
    const table = screen.getByRole('table', { hidden: true });
    expect(within(table).getAllByRole('row', { hidden: true })).toHaveLength(points.length + 1);
    expect(within(table).getByText('₱250')).toBeInTheDocument();
  });

  it('reads a day with the arrow keys, and stops at the ends', () => {
    chart();
    const group = screen.getByLabelText(/left and right arrow keys/);
    fireEvent.keyDown(group, { key: 'End' });
    expect(screen.getByRole('tooltip')).toHaveTextContent('Sep 24');
    expect(screen.getByRole('tooltip')).toHaveTextContent('₱400');
    fireEvent.keyDown(group, { key: 'ArrowRight' });
    expect(screen.getByRole('tooltip')).toHaveTextContent('Sep 24');
    fireEvent.keyDown(group, { key: 'ArrowLeft' });
    expect(screen.getByRole('tooltip')).toHaveTextContent('Sep 23');
    fireEvent.keyDown(group, { key: 'Home' });
    expect(screen.getByRole('tooltip')).toHaveTextContent('Sep 20');
  });

  it('hides the readout when focus leaves', () => {
    chart();
    const group = screen.getByLabelText(/left and right arrow keys/);
    fireEvent.keyDown(group, { key: 'End' });
    fireEvent.blur(group);
    expect(screen.queryByRole('tooltip')).toBeNull();
  });

  it('copes with a single day and with no sales at all', () => {
    const { rerender } = render(<AreaLineChart points={[points[0]]} seriesName="Revenue" formatValue={peso} formatAxis={peso} ariaLabel="One day" />);
    expect(screen.getByLabelText(/left and right arrow keys/)).toBeInTheDocument();
    rerender(<AreaLineChart points={points.map((p) => ({ ...p, value: 0 }))} seriesName="Revenue" formatValue={peso} formatAxis={peso} ariaLabel="Nothing sold" />);
    expect(screen.queryByText(/^Best:/)).toBeNull();
  });
});

describe('CalendarHeatmap', () => {
  it('draws one cell per day and says what darker means', () => {
    const { container } = render(<CalendarHeatmap points={points} seriesName="Revenue" formatValue={peso} ariaLabel="Days" />);
    expect(container.querySelectorAll('svg rect')).toHaveLength(points.length);
    expect(screen.getByText('Less')).toBeInTheDocument();
    expect(screen.getByText('More')).toBeInTheDocument();
  });

  it('shows a day on hover, including a day with nothing sold', () => {
    const { container } = render(<CalendarHeatmap points={points} seriesName="Revenue" formatValue={peso} ariaLabel="Days" />);
    const cells = container.querySelectorAll('svg rect');
    fireEvent.pointerEnter(cells[2]);
    expect(screen.getByRole('tooltip')).toHaveTextContent('Sep 22');
    expect(screen.getByRole('tooltip')).toHaveTextContent('₱0');
    fireEvent.pointerLeave(cells[2]);
    expect(screen.queryByRole('tooltip')).toBeNull();
  });

  it('draws the busiest day darkest and an empty day as an outline', () => {
    const { container } = render(<CalendarHeatmap points={points} seriesName="Revenue" formatValue={peso} ariaLabel="Days" />);
    const fills = [...container.querySelectorAll('svg rect')].map((r) => r.getAttribute('fill'));
    expect(fills[3]).toContain('100%');
    expect(fills[2]).toBe('transparent');
  });
});

describe('Donut', () => {
  const slices = [
    { key: 'a', label: 'Coffee bar', value: 600, color: 'var(--viz-1)' },
    { key: 'b', label: 'Kitchen', value: 300, color: 'var(--viz-2)' },
    { key: 'c', label: 'Retail', value: 5, color: 'var(--viz-3)' },
  ];

  it('lists every part with its value and share, so colour is never the only channel', () => {
    render(<Donut slices={slices} formatValue={peso} totalLabel="Total" totalValue="₱905" ariaLabel="Revenue by department" />);
    expect(screen.getByText('₱600, 66%')).toBeInTheDocument();
    expect(screen.getByText('₱5, <1%')).toBeInTheDocument();
    expect(screen.getByText('₱905')).toBeInTheDocument();
  });

  it('shows a part on hover and skips parts with nothing in them', () => {
    const { container } = render(<Donut slices={[...slices, { key: 'z', label: 'Unused', value: 0, color: 'var(--viz-4)' }]} formatValue={peso} totalLabel="Total" totalValue="₱905" ariaLabel="x" />);
    const paths = container.querySelectorAll('svg path');
    expect(paths).toHaveLength(3);
    fireEvent.pointerEnter(paths[1]);
    expect(screen.getByRole('tooltip')).toHaveTextContent('Kitchen');
    expect(screen.getByRole('tooltip')).toHaveTextContent('₱300');
  });
});

describe('StackedStrip and Waffle', () => {
  it('splits a strip in proportion and lists each part', () => {
    render(
      <StackedStrip
        formatValue={peso}
        ariaLabel="By branch"
        items={[
          { key: 'a', label: 'Poblacion', value: 75, color: 'var(--viz-1)' },
          { key: 'b', label: 'Katipunan', value: 25, color: 'var(--viz-2)' },
          { key: 'c', label: 'Empty', value: 0, color: 'var(--viz-3)' },
        ]}
      />,
    );
    const bar = screen.getByRole('img', { name: 'By branch' });
    expect(bar.children).toHaveLength(2);
    expect((bar.children[0] as HTMLElement).style.width).toBe('75%');
    expect(screen.getByText('₱25, 25%')).toBeInTheDocument();
    expect(screen.getByText('Empty')).toBeInTheDocument();
  });

  it('draws one square per person, with switched-off people as outlines', () => {
    const { container } = render(
      <Waffle
        unit="staff"
        ariaLabel="Staff by role"
        items={[
          { key: 'c', label: 'Cashier', count: 3, color: 'var(--viz-3)' },
          { key: 'c-off', label: 'Cashier, switched off', count: 1, color: 'var(--viz-3)', hollow: true },
        ]}
      />,
    );
    expect(screen.getByRole('img', { name: 'Staff by role: 4 staff' })).toBeInTheDocument();
    expect(container.querySelectorAll('span[title="Cashier"]')).toHaveLength(3);
    expect(container.querySelectorAll('span[title="Cashier, switched off"]')).toHaveLength(1);
  });
});

describe('ranked charts', () => {
  const rows = [
    { key: 'a', label: 'Tapsilog', value: 1000, detail: '95 sold' },
    { key: 'b', label: 'Latte', value: 500 },
    { key: 'c', label: 'Nothing', value: 0 },
  ];

  it('scales bars against the largest and states every value', () => {
    const { container } = render(<BarList rows={rows} formatValue={peso} />);
    const widths = [...container.querySelectorAll<HTMLElement>('li > div > div')].map((b) => b.style.width);
    expect(widths).toEqual(['100%', '50%', '0%']);
    expect(screen.getByText('₱1000')).toBeInTheDocument();
    expect(screen.getByText('95 sold')).toBeInTheDocument();
  });

  it('renders lollipops and dots with the same figures', () => {
    render(<LollipopList rows={rows} formatValue={peso} />);
    expect(screen.getByText('₱500')).toBeInTheDocument();
    render(<DotPlot rows={rows} formatValue={peso} />);
    expect(screen.getAllByText('₱1000')).toHaveLength(2);
  });

  it('flags stock at or under its alert level, and out of stock in words', () => {
    render(
      <BulletList
        mode="floor"
        rows={[
          { key: 'a', label: 'Latte', value: 5, target: 20 },
          { key: 'b', label: 'Cookie', value: 40, target: 20 },
          { key: 'c', label: 'Tumbler', value: 0, target: 10 },
        ]}
        describe={(r) => (r.value <= 0 ? { value: 'Out', note: 'of stock', alert: true } : { value: String(r.value), note: `left, alert at ${r.target}` })}
      />,
    );
    expect(screen.getByRole('img', { name: 'Latte: 5 left, alert at 20' })).toBeInTheDocument();
    expect(screen.getByText('Out')).toHaveClass('text-danger');
    expect(screen.getByRole('img', { name: 'Cookie: 40 left, alert at 20' }).children[1]).toHaveStyle({ background: 'var(--viz-accent)' });
    expect(screen.getByRole('img', { name: 'Latte: 5 left, alert at 20' }).children[1]).toHaveStyle({ background: 'var(--color-warn)' });
  });

  it('warns as credit nears its limit and turns red once it is past it', () => {
    render(
      <BulletList
        mode="ceiling"
        rows={[
          { key: 'a', label: 'Low', value: 100, target: 1000 },
          { key: 'b', label: 'Near', value: 900, target: 1000 },
          { key: 'c', label: 'Over', value: 1200, target: 1000 },
        ]}
        describe={(r) => ({ value: peso(r.value), note: `of ${peso(r.target)}` })}
      />,
    );
    const bar = (name: string) => screen.getByRole('img', { name }).children[1];
    expect(bar('Low: ₱100 of ₱1000')).toHaveStyle({ background: 'var(--viz-accent)' });
    expect(bar('Near: ₱900 of ₱1000')).toHaveStyle({ background: 'var(--color-warn)' });
    expect(bar('Over: ₱1200 of ₱1000')).toHaveStyle({ background: 'var(--color-danger)' });
  });
});

describe('Meter', () => {
  it('exposes the count as a meter and pairs colour with words', () => {
    render(<Meter label="Out of stock" value={2} total={15} unit="items" tone="danger" status="2 items cannot be sold" />);
    const meter = screen.getByRole('meter', { name: 'Out of stock' });
    expect(meter).toHaveAttribute('aria-valuenow', '2');
    expect(meter).toHaveAttribute('aria-valuemax', '15');
    expect(screen.getByText('2 items cannot be sold')).toBeInTheDocument();
    expect(screen.getByText(/of 15 items/)).toBeInTheDocument();
  });

  it('is empty, not broken, with a zero total', () => {
    render(<Meter label="Nothing" value={0} total={0} unit="items" />);
    expect(screen.getByRole('meter', { name: 'Nothing' }).firstElementChild).toHaveStyle({ width: '0%' });
  });
});

describe('TimelineDots', () => {
  const now = Date.parse('2026-09-24T12:00:00Z');
  it('puts each device on the week axis and says when it was seen', () => {
    render(
      <TimelineDots
        now={now}
        rows={[
          { key: 'a', label: 'Front counter', detail: 'Register', seenAt: '2026-09-24T11:50:00Z' },
          { key: 'b', label: 'Kitchen screen', seenAt: '2026-09-21T12:00:00Z' },
          { key: 'c', label: 'Order board', seenAt: null },
        ]}
      />,
    );
    expect(screen.getByText('10 min ago')).toBeInTheDocument();
    expect(screen.getByText('3 days ago')).toBeInTheDocument();
    expect(screen.getByText('Never seen')).toBeInTheDocument();
    expect(screen.getByText('Register')).toBeInTheDocument();
  });
});
