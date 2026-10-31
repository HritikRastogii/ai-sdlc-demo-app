import { test, expect } from '@playwright/test';

test('Manager cannot approve an invoice above 50000', async ({ page, request }) => {
  const username = process.env.E2E_MANAGER_USERNAME || 'demo.manager';
  const password = process.env.E2E_MANAGER_PASSWORD;

  test.skip(!password, 'Set E2E_MANAGER_PASSWORD before running this test.');

  // Create and submit an invoice just above the Manager limit.
  const createResponse = await request.post('/api/invoices', {
    data: {
      customerName: `E2E Manager Limit ${Date.now()}`,
      amount: 50001
    }
  });

  expect(createResponse.status()).toBe(201);
  const invoice = await createResponse.json();

  const submitResponse = await request.post(
    `/api/invoices/${invoice.id}/submit`
  );
  expect(submitResponse.status()).toBe(200);

  // Sign in through the browser as Manager.
  await page.goto('/');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.locator('#whoami')).toContainText('Manager');

  // Manager must not see an approval button for this invoice.
  await page.getByRole('tab', { name: 'Approvals' }).click();

  await expect(
    page.getByRole('button', { name: `Approve invoice ${invoice.id}` })
  ).toHaveCount(0);

  // Also verify that a direct API attempt is rejected.
  const session = await page.evaluate(() =>
    JSON.parse(sessionStorage.getItem('session') || 'null')
  );

  const approvalResponse = await request.post(
    `/api/Approvals/${invoice.id}/approve`,
    {
      headers: {
        Authorization: `Bearer ${session.token}`
      }
    }
  );

  expect(approvalResponse.status()).toBe(409);

  const errorBody = await approvalResponse.json();
  expect(errorBody.error).toContain('Manager cannot approve invoice');

  // Verify the rejected attempt did not change the invoice.
  const getResponse = await request.get(`/api/invoices/${invoice.id}`);
  expect(getResponse.status()).toBe(200);

  const unchangedInvoice = await getResponse.json();

  expect(unchangedInvoice.status).toBe('PendingApproval');
  expect(unchangedInvoice.approvalHistory).toHaveLength(0);
  expect(unchangedInvoice.decidedBy).toBeNull();
  expect(unchangedInvoice.decidedAt).toBeNull();
});
