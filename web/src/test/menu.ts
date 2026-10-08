import { fireEvent, screen } from '@testing-library/react';

const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** Opens the ⋯ menu that belongs to `subject` and picks `item`. An item's hint (why it is unavailable) is ignored when matching. */
export async function chooseFromMenu(subject: string, item: string): Promise<void> {
  fireEvent.click(await screen.findByRole('button', { name: `Actions for ${subject}` }));
  fireEvent.click(await screen.findByRole('menuitem', { name: new RegExp(`^${escape(item)}`) }));
}

/** Opens the ⋯ menu that belongs to `subject` and returns its items' names, for checking what is and is not offered. */
export async function menuItemNames(subject: string): Promise<string[]> {
  fireEvent.click(await screen.findByRole('button', { name: `Actions for ${subject}` }));
  return (await screen.findAllByRole('menuitem')).map((el) => el.textContent ?? '');
}
