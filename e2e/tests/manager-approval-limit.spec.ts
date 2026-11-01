import { test, expect } from '@playwright/test';

test('Manager cannot approve an invoice above 50000', async ({ page, request }) => {
  const username = process.env.E2E_MANAGER_USERNAME || 'demo.manager';
  const password = process.env.E2E_MANAGER_PASSWORD;

  test.skip(!password, 'Set E2E_MANAGER_PASSWORD before running this test.');

  const loginResponse = await request.post('/api/auth/login', {
    data: { username, password }
  });

  expect(loginResponse.status()).toBe(200);
  const apiSession = await loginResponse.json();
  const authorization = { Authorization: `Bearer ${apiSession.token}` };

  const createResponse = await request.post('/api/invoices', {
    headers: authorization,
    data: {
      customerName: `E2E Manager Limit ${Date.now()}`,
      amount: 50001
    }
  });

  expect(createResponse.status()).toBe(201);
  const invoice = await createResponse.json();

  const submitResponse = await request.post(
    `/api/invoices/${invoice.id}/submit`,
    { headers: authorization }
  );
  expect(submitResponse.status()).toBe(200);

  const beforeApprovalResponse = await request.get(`/api/invoices/${invoice.id}`, {
    headers: authorization
  });
  expect(beforeApprovalResponse.status()).toBe(200);
  const beforeApproval = await beforeApprovalResponse.json();

  await page.goto('/');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.locator('#whoami')).toContainText('Manager');
  await page.getByRole('tab', { name: 'Approvals' }).click();

  await page.getByRole('button', { name: `Approve invoice ${invoice.id}` }).click();
  await expect(page.locator('#decision-error'))
    .toContainText('Manager cannot approve invoice');

  const getResponse = await request.get(`/api/invoices/${invoice.id}`, {
    headers: authorization
  });
  expect(getResponse.status()).toBe(200);

  const unchangedInvoice = await getResponse.json();
  expect(unchangedInvoice.status).toBe(beforeApproval.status);
  expect(unchangedInvoice.approvalHistory).toEqual(beforeApproval.approvalHistory);
  expect(unchangedInvoice.decidedBy).toBe(beforeApproval.decidedBy);
  expect(unchangedInvoice.decidedAt).toBe(beforeApproval.decidedAt);
});
