import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { PageHeader } from './PageHeader';

describe('PageHeader', () => {
  it('goes back to the page one level up, not to browser history, and is large enough to tap', () => {
    render(
      <MemoryRouter>
        <PageHeader title="Modifier groups" backTo={{ to: '/business', label: 'Business' }} />
      </MemoryRouter>,
    );
    const back = screen.getByRole('link', { name: 'Back to Business' });
    expect(back).toHaveAttribute('href', '/business');
    expect(back).toHaveClass('h-12');
    expect(screen.getByRole('heading', { name: 'Modifier groups' })).toBeInTheDocument();
  });

  it('has no back link on a landing page', () => {
    render(
      <MemoryRouter>
        <PageHeader title="Business" />
      </MemoryRouter>,
    );
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
