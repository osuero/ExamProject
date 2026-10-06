import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { ADMIN, keyFor, signIn, startAttempt, uniqueEmail } from './helpers';

test.describe('catalog', () => {
  test('cards show format, language and a quick look on hover and keyboard focus', async ({ page }) => {
    await page.goto('/exams');
    const cards = page.locator('app-exam-card');
    await expect(cards).toHaveCount(2);
    const dev = cards.filter({ hasText: 'CCDV-F' });
    await expect(dev).toContainText('53 questions in 120 minutes');
    await expect(dev).toContainText('English');
    const preview = dev.getByRole('region', { name: /Quick look/ });
    await expect(preview).toBeHidden();
    await dev.hover();
    await expect(preview).toBeVisible();
    await expect(preview).toContainText('Applications and Integration');
    await page.mouse.move(0, 0);
    await expect(preview).toBeHidden();
    await dev.getByRole('link', { name: /Developer/ }).focus();
    await expect(preview).toBeVisible();
  });

  test('detail page shows syllabus, weights and modes', async ({ page }) => {
    await page.goto('/exams/CCAR-F');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(/Architect/);
    await expect(page.getByRole('heading', { name: 'Agentic Architecture & Orchestration' })).toBeVisible();
    await expect(page.getByText('27%')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Sign in to start' })).toBeVisible();
  });

  test('quick look works by tap on mobile @mobile', async ({ page }) => {
    await page.goto('/exams');
    const card = page.locator('app-exam-card').first();
    const preview = card.getByRole('region', { name: /Quick look/ });
    await expect(preview).toBeHidden();
    await card.getByRole('button', { name: 'Quick look' }).tap();
    await expect(preview).toBeVisible();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    expect(overflow).toBe(false);
  });

  test('pages have no serious accessibility violations', async ({ page }) => {
    for (const url of ['/', '/exams', '/exams/CCDV-F', '/login']) {
      await page.goto(url);
      await page.waitForLoadState('networkidle');
      const r = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
      const serious = r.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
      expect(serious.map((v) => `${url}: ${v.id} ${v.nodes.length}`)).toEqual([]);
    }
  });
});

test.describe('account', () => {
  test('magic link sign-in works once and the link cannot be reused', async ({ page, browser }) => {
    const email = uniqueEmail('login');
    await page.goto('/login');
    await page.getByLabel('Email address').fill(email);
    await page.getByRole('button', { name: 'Send sign-in link' }).click();
    await expect(page.getByText('Check your inbox.')).toBeVisible();
    const r = await page.request.get(`/api/dev/outbox?to=${encodeURIComponent(email)}`);
    const link: string = (await r.json())[0].body.match(/https?:\/\/\S+\/auth\/verify#token=[\w-]+/)[0];
    await page.goto(link.replace(/^https?:\/\/[^/]+/, ''));
    await expect(page).toHaveURL(/\/my$/);
    await expect(page.getByText(email)).toBeVisible();
    expect(page.url()).not.toContain('token=');

    const other = await browser.newPage();
    await other.goto(link.replace(/^https?:\/\/[^/]+/, ''));
    await expect(other.getByRole('heading', { name: 'That link did not work' })).toBeVisible();
    await other.close();
  });

  test('invalid email is explained', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email address').fill('not-an-email');
    await page.getByRole('button', { name: 'Send sign-in link' }).click();
    await expect(page.getByRole('alert')).toContainText('valid email');
  });
});

test.describe('practice', () => {
  test('single answer: correct and incorrect feedback with explanations for every option', async ({ page }) => {
    await signIn(page, ADMIN);
    const id = await startAttempt(page, { certificationCode: 'CCDV-F', mode: 'practice', questionCount: 6, feedback: true, domainCodes: ['D2'] });
    await page.goto(`/attempts/${id}`);
    const state = await (await page.request.get(`/api/attempts/${id}`)).json();
    const singles = state.items.filter((i: any) => i.questionType === 'single_choice').slice(0, 2);

    // first single: answer correctly
    const [a, b] = singles;
    await page.getByRole('button', { name: new RegExp(`^Question ${a.position},`) }).click();
    const keyA = await keyFor(page, 'CCDV-F', a.questionRef.externalId);
    const labelA = a.options.find((o: any) => o.id === keyA[0]).label;
    await page.locator('label.opt', { hasText: a.options.find((o: any) => o.id === keyA[0]).text }).click();
    await expect(page.getByText('Answer saved')).toBeVisible();
    await page.getByRole('button', { name: 'Check answer' }).click();
    await expect(page.getByRole('heading', { name: 'Correct.' })).toBeVisible();
    await expect(page.locator('.verdict')).toHaveCount(a.options.length);
    expect(labelA).toBeTruthy();

    // second single: answer incorrectly
    await page.getByRole('button', { name: new RegExp(`^Question ${b.position},`) }).click();
    const keyB = await keyFor(page, 'CCDV-F', b.questionRef.externalId);
    const wrong = b.options.find((o: any) => !keyB.includes(o.id));
    await page.locator('label.opt', { hasText: wrong.text }).click();
    await expect(page.getByText('Answer saved')).toBeVisible();
    await page.getByRole('button', { name: 'Check answer' }).click();
    await expect(page.getByRole('heading', { name: 'Not correct.' })).toBeVisible();
    const right = b.options.find((o: any) => o.id === keyB[0]);
    await expect(page.getByText(`The correct answer is ${right.label}.`)).toBeVisible();
    await expect(page.locator('.feedback a').first()).toHaveAttribute('href', /^https:\/\//);
    await expect(page.getByText('Assisted', { exact: true })).toBeVisible();
  });

  test('multiple response waits for the full selection and an explicit check', async ({ page }) => {
    await signIn(page, ADMIN);
    const id = await startAttempt(page, { certificationCode: 'CCDV-F', mode: 'practice', questionCount: 40, feedback: true });
    const state = await (await page.request.get(`/api/attempts/${id}`)).json();
    const multi = state.items.find((i: any) => i.questionType === 'multiple_response');
    await page.goto(`/attempts/${id}`);
    await page.getByRole('button', { name: new RegExp(`^Question ${multi.position},`) }).click();
    await expect(page.getByText('Select 2')).toBeVisible();
    const check = page.getByRole('button', { name: 'Check answer' });
    await page.locator('label.opt').nth(0).click();
    await expect(page.getByText('1 of 2 selected')).toBeVisible();
    await expect(check).toBeDisabled();
    await expect(page.locator('.feedback')).toHaveCount(0);
    await page.locator('label.opt').nth(1).click();
    await expect(page.getByText('2 of 2 selected')).toBeVisible();
    // a third option cannot be added
    await expect(page.locator('label.opt').nth(2).locator('input')).toBeDisabled();
    await check.click();
    await expect(page.locator('.feedback')).toBeVisible();
  });
});

test.describe('simulation', () => {
  test('no solutions in the page or API while active; turning feedback on marks it assisted', async ({ page }) => {
    const email = uniqueEmail('sim');
    await signIn(page, email);
    const bodies: string[] = [];
    page.on('response', async (r) => { if (r.url().includes('/api/attempts')) { try { bodies.push(await r.text()); } catch { /* ignore */ } } });
    await page.goto('/exams/CCAR-F');
    await page.getByLabel(/Simulation/).check();
    await page.getByRole('button', { name: 'Start simulation' }).click();
    await expect(page).toHaveURL(/\/attempts\//);
    await expect(page.getByText('Question 1 of 60')).toBeVisible();
    await expect(page.getByRole('timer')).toHaveText(/1:5\d:\d\d|2:00:00/);
    await expect(page.locator('.scenario').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Check answer' })).toHaveCount(0);
    const html = await page.content();
    for (const secret of ['correctOptionIds', 'rationale', 'isCorrect']) {
      expect(html).not.toContain(secret);
      expect(bodies.join('\n')).not.toContain(`"${secret}"`);
    }
    await expect(page.getByText('Clean', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Turn on feedback…' }).click();
    await page.getByRole('button', { name: 'Show solutions' }).click();
    await expect(page.getByText('Assisted', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: /Hide feedback/ }).click();
    await expect(page.getByText('Assisted', { exact: true })).toBeVisible();
  });

  test('timer survives reload, answers autosave, and the map tracks state', async ({ page }) => {
    await signIn(page, uniqueEmail('timer'));
    const id = await startAttempt(page, { certificationCode: 'CCDV-F', mode: 'custom', questionCount: 5, durationMinutes: 30, feedback: false });
    await page.goto(`/attempts/${id}`);
    await page.locator('label.opt').first().click();
    await expect(page.getByText('Answer saved')).toBeVisible();
    await page.getByRole('button', { name: 'Mark for review' }).click();
    await expect(page.getByRole('button', { name: /^Question 1, answered, marked for review/ })).toBeVisible();
    await page.waitForTimeout(3000);
    const before = await page.getByRole('timer').innerText();
    await page.reload();
    const after = await page.getByRole('timer').innerText();
    const secs = (t: string) => t.split(':').reduce((a, x) => a * 60 + +x, 0);
    expect(secs(after)).toBeLessThanOrEqual(secs(before));
    expect(secs(after)).toBeGreaterThan(secs('29:00'));
    await page.getByRole('button', { name: /^Question 1,/ }).click();
    await expect(page.locator('label.opt').first().locator('input')).toBeChecked();
  });

  test('connection loss keeps the choice and retries; finish shows the result', async ({ page, context }) => {
    await signIn(page, uniqueEmail('offline'));
    const id = await startAttempt(page, { certificationCode: 'CCDV-F', mode: 'custom', questionCount: 3, durationMinutes: 20, feedback: false });
    await page.goto(`/attempts/${id}`);
    await expect(page.locator('label.opt').nth(1)).toBeVisible();
    await context.setOffline(true);
    await page.locator('label.opt').nth(1).click();
    await expect(page.getByText(/Connection lost/)).toBeVisible();
    await context.setOffline(false);
    await expect(page.getByText('Answer saved')).toBeVisible({ timeout: 10_000 });
    await page.getByRole('button', { name: 'Finish attempt' }).click();
    await expect(page.getByText(/2 unanswered/)).toBeVisible();
    await page.getByRole('button', { name: 'Finish and see result' }).click();
    await expect(page).toHaveURL(/\/result$/);
    await expect(page.getByText(/of 3 points/)).toBeVisible();
    await expect(page.getByText('Clean run')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'By domain' })).toBeVisible();
    await expect(page.getByText(/not an official Pearson VUE/)).toBeVisible();
  });
});

test.describe('isolation and admin', () => {
  test('another user cannot open my attempt', async ({ page, browser }) => {
    await signIn(page, uniqueEmail('owner'));
    const id = await startAttempt(page, { certificationCode: 'CCDV-F', mode: 'practice', questionCount: 2 });
    const ctx2 = await browser.newContext();
    const p2 = await ctx2.newPage();
    await signIn(p2, uniqueEmail('intruder'));
    await p2.goto(`/attempts/${id}`);
    await expect(p2.getByRole('alert')).toContainText('does not exist or belongs to another account');
    expect((await p2.request.get(`/api/attempts/${id}`)).status()).toBe(404);
    await ctx2.close();
  });

  test('students cannot open admin; admins see questions, coverage and import preview', async ({ page }) => {
    await signIn(page, uniqueEmail('student'));
    await page.goto('/admin');
    await expect(page).toHaveURL(/\/$/);
    expect((await page.request.get('/api/admin/questions?certificationCode=CCDV-F')).status()).toBe(403);

    await page.getByRole('button', { name: 'Sign out' }).click();
    await signIn(page, ADMIN);
    await page.goto('/admin');
    await expect(page.getByRole('heading', { name: 'Administration' })).toBeVisible();
    await expect(page.locator('table.data tbody tr').first()).toBeVisible();
    await page.getByRole('tab', { name: 'Coverage and quality' }).click();
    await expect(page.getByText(/Editorial target/)).toBeVisible();
    await page.getByRole('tab', { name: 'Import' }).click();
    await page.locator('input[type=file]').setInputFiles({
      name: 'sample.md', mimeType: 'text/markdown',
      buffer: Buffer.from(`---\nexamCode: CCDV-F\n---\n## Question E2E-MD-001\n- domain: D1\n- objective: Agent Architecture\n- type: single_choice\n- select: 1\n- sources: T01\n\nA unique end-to-end preview stem about choosing between a fixed workflow and an agent for invoices.\n\n### Options\n- [x] A: Fixed workflow | rationale: Known steps.\n- [ ] B: Autonomous agent | rationale: Unneeded discretion.\n- [ ] C: Two agents voting | rationale: Adds variance.\n- [ ] D: One prompt only | rationale: No enforcement.\n\n### Explanation\nKnown steps favour workflows.\n`),
    });
    await page.getByRole('button', { name: 'Preview' }).click();
    await expect(page.getByText('Ready to import.')).toBeVisible();
    await expect(page.getByRole('cell', { name: 'E2E-MD-001' })).toBeVisible();
  });
});

test.describe('accessibility of signed-in pages', () => {
  test('runner with feedback, result, my exams and admin have no serious violations', async ({ page }) => {
    const scan = async (label: string) => {
      const r = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
      return r.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical').map((v) => `${label}: ${v.id} (${v.nodes.length})`);
    };
    await signIn(page, ADMIN);
    const found: string[] = [];
    const id = await startAttempt(page, { certificationCode: 'CCAR-F', mode: 'practice', questionCount: 3, feedback: true });
    await page.goto(`/attempts/${id}`);
    await page.locator('label.opt').first().click();
    await expect(page.getByText('Answer saved')).toBeVisible();
    found.push(...(await scan('runner')));
    await page.getByRole('button', { name: 'Check answer' }).click();
    await expect(page.locator('.feedback')).toBeVisible();
    found.push(...(await scan('runner-feedback')));
    await page.getByRole('button', { name: 'Finish attempt' }).click();
    await page.getByRole('button', { name: 'Finish and see result' }).click();
    await expect(page).toHaveURL(/\/result$/);
    found.push(...(await scan('result')));
    await page.goto('/my');
    await expect(page.getByRole('heading', { name: 'My exams' })).toBeVisible();
    found.push(...(await scan('my-exams')));
    await page.goto('/progress/CCAR-F');
    await expect(page.getByRole('heading', { name: 'CCAR-F progress' })).toBeVisible();
    found.push(...(await scan('progress')));
    await page.goto('/admin');
    await expect(page.locator('table.data tbody tr').first()).toBeVisible();
    found.push(...(await scan('admin')));
    expect(found).toEqual([]);
  });
});
