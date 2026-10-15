import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ParetoChart } from './ParetoChart';
import { ScatterChart } from './ScatterChart';
import { StackedColumnChart } from './StackedColumnChart';
import { WaterfallChart } from './WaterfallChart';

const peso = (n: number) => `₱${n}`;

describe('WaterfallChart', () => {
  it('labels each step and carries the running total in a table', () => {
    render(
      <WaterfallChart
        steps={[
          { key: 'g', label: 'Gross', value: 1000, kind: 'total' },
          { key: 'd', label: 'Discounts', value: -150, kind: 'delta' },
          { key: 'n', label: 'Net', value: 850, kind: 'total' },
        ]}
        formatValue={peso}
        formatAxis={peso}
        ariaLabel="Gross to net"
      />,
    );

    expect(screen.getByText('-₱150')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Show as table' }));
    const table = screen.getByRole('table', { hidden: true });
    // The running total after the discount, and the net step's amount and running total.
    expect(within(table).getAllByText('₱850', { selector: 'td' })).toHaveLength(3);
  });
});

describe('ParetoChart', () => {
  it('shows the running share for each bar in its table', () => {
    render(
      <ParetoChart
        rows={[
          { key: 'a', label: 'Drinks', value: 600, cumulativePercent: 60 },
          { key: 'b', label: 'Snacks', value: 400, cumulativePercent: 100 },
        ]}
        seriesName="Sales"
        formatValue={peso}
        formatAxis={peso}
        ariaLabel="Sales by category"
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Show as table' }));
    const table = screen.getByRole('table', { hidden: true });
    expect(within(table).getByText('60%')).toBeInTheDocument();
    expect(within(table).getByText('100%')).toBeInTheDocument();
  });
});

describe('StackedColumnChart', () => {
  it('has a legend and one table column per series', () => {
    render(
      <StackedColumnChart
        points={[
          { key: '1', label: 'Sep 1', values: { a: 10, b: 5 } },
          { key: '2', label: 'Sep 2', values: { a: 20, b: 0 } },
        ]}
        series={[
          { key: 'a', label: 'Discounts', color: 'red' },
          { key: 'b', label: 'Refunds', color: 'blue' },
        ]}
        formatValue={peso}
        formatAxis={peso}
        ariaLabel="Leakage by day"
      />,
    );

    expect(screen.getAllByText('Discounts').length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: 'Show as table' }));
    expect(within(screen.getByRole('table', { hidden: true })).getAllByRole('columnheader', { hidden: true })).toHaveLength(3);
  });
});

describe('ScatterChart', () => {
  it('names the quadrant each point falls in', () => {
    render(
      <ScatterChart
        points={[
          { key: 'a', label: 'Drinks', x: 900, y: 40 },
          { key: 'b', label: 'Snacks', x: 100, y: 10 },
          { key: 'c', label: 'Bakery', x: 500, y: 25 },
        ]}
        xName="Sales"
        yName="Margin"
        formatX={peso}
        formatY={(v) => `${v}%`}
        ariaLabel="Categories by sales and margin"
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Show as table' }));
    const table = screen.getByRole('table', { hidden: true });
    expect(within(table).getAllByText('Strong on both').length).toBeGreaterThan(0);
    expect(within(table).getByText('Weak on both')).toBeInTheDocument();
  });
});
