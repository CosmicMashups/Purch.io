import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { RequireTab } from './RequireTab';

function guarded(tab: 'home' | 'sell' | 'inventory' | 'business') {
  return renderPage(<RequireTab tab={tab} />, {
    route: '/screen',
    path: '/screen',
    otherRoutes: [
      { path: '/', element: <p>Dashboard</p> },
      { path: '/sell', element: <p>Cashier page</p> },
      { path: '/inventory', element: <p>Inventory page</p> },
    ],
  });
}

describe('RequireTab', () => {
  it('sends a Cashier who asks for Home straight to the Cashier page', () => {
    signInAs('Cashier');
    guarded('home');
    expect(screen.getByText('Cashier page')).toBeInTheDocument();
  });

  it('sends a Warehouse user who asks for the Cashier page to Inventory', () => {
    signInAs('Warehouse');
    guarded('sell');
    expect(screen.getByText('Inventory page')).toBeInTheDocument();
  });

  it('sends a Cashier who asks for Inventory to the Cashier page', () => {
    signInAs('Cashier');
    guarded('inventory');
    expect(screen.getByText('Cashier page')).toBeInTheDocument();
  });

  it('lets an Admin or Manager through to Home', () => {
    signInAs('Manager');
    const { container } = guarded('home');
    expect(container).toBeEmptyDOMElement();
  });
});
