import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { Skeleton, SkeletonRows, SkeletonList } from '../Skeleton';

describe('Skeleton', () => {
  it('renders a single skeleton bar with default classes', () => {
    const { container } = render(<Skeleton />);
    const bar = container.firstChild as HTMLElement;
    expect(bar).toBeInTheDocument();
    expect(bar).toHaveClass('animate-pulse', 'rounded', 'bg-gray-200');
  });

  it('applies custom className', () => {
    const { container } = render(<Skeleton className="h-6 w-32 custom-class" />);
    const bar = container.firstChild as HTMLElement;
    expect(bar).toHaveClass('h-6', 'w-32', 'custom-class');
  });
});

describe('SkeletonRows', () => {
  it('renders default 5 rows with 4 columns each', () => {
    const { container } = render(<SkeletonRows />);
    const root = container.firstChild as HTMLElement;
    expect(root).toHaveAttribute('aria-hidden', 'true');
    expect(root.children).toHaveLength(5);
    for (let i = 0; i < root.children.length; i++) {
      expect(root.children[i].children).toHaveLength(4);
    }
  });

  it('renders custom number of rows and columns', () => {
    const { container } = render(<SkeletonRows rows={3} columns={2} />);
    const root = container.firstChild as HTMLElement;
    expect(root.children).toHaveLength(3);
    for (let i = 0; i < root.children.length; i++) {
      expect(root.children[i].children).toHaveLength(2);
    }
  });
});

describe('SkeletonList', () => {
  it('renders default 4 rows with 2 skeleton bars each', () => {
    const { container } = render(<SkeletonList />);
    const root = container.firstChild as HTMLElement;
    expect(root).toHaveAttribute('aria-hidden', 'true');
    expect(root.children).toHaveLength(4);
    for (let i = 0; i < root.children.length; i++) {
      expect(root.children[i].children).toHaveLength(2);
    }
  });

  it('renders specified number of rows', () => {
    const { container } = render(<SkeletonList rows={6} />);
    const root = container.firstChild as HTMLElement;
    expect(root.children).toHaveLength(6);
  });
});
