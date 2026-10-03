import { APIRequestContext, expect, Page } from '@playwright/test';

export const ADMIN = 'admin@example.test';

export function uniqueEmail(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.test`;
}

/** Signs in through the real UI: request link, read it from the development mailbox, open it. */
export async function signIn(page: Page, email: string) {
  await page.goto('/login');
  await page.getByLabel('Email address').fill(email);
  await page.getByRole('button', { name: 'Send sign-in link' }).click();
  await expect(page.getByText('Check your inbox.')).toBeVisible();
  const link = await latestLink(page.request, email);
  await page.goto(link.replace(/^https?:\/\/[^/]+/, ''));
  await expect(page).toHaveURL(/\/my$/);
}

export async function latestLink(request: APIRequestContext, email: string): Promise<string> {
  for (let i = 0; i < 20; i++) {
    const r = await request.get(`/api/dev/outbox?to=${encodeURIComponent(email)}`);
    const mails = (await r.json()) as { body: string }[];
    const m = mails[0]?.body.match(/https?:\/\/\S+\/auth\/verify#token=[\w-]+/);
    if (m) return m[0];
    await new Promise((res) => setTimeout(res, 250));
  }
  throw new Error('No sign-in mail for ' + email);
}

export async function xsrf(page: Page) {
  const cookies = await page.context().cookies();
  return cookies.find((c) => c.name === 'XSRF-TOKEN')?.value ?? '';
}

/** Starts an attempt through the API with the browser session (same cookies as the UI). */
export async function startAttempt(page: Page, body: Record<string, unknown>): Promise<string> {
  const r = await page.request.post('/api/attempts', { data: body, headers: { 'X-XSRF-TOKEN': await xsrf(page) } });
  expect(r.status(), await r.text()).toBe(201);
  return (await r.json()).id;
}

/** Admin-only lookup of the stored answer key for an item (used to drive correct/incorrect paths). */
export async function keyFor(page: Page, cert: string, externalId: string): Promise<string[]> {
  const list = await (await page.request.get(`/api/admin/questions?certificationCode=${cert}`)).json();
  const row = list.find((q: any) => q.externalId === externalId);
  const d = await (await page.request.get(`/api/admin/questions/${row.questionId}`)).json();
  return d.latest.correctOptionIds;
}
