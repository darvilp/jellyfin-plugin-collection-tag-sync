import { expect, test } from '@playwright/test';

const pluginId = '04920eee-c499-4b13-890f-7af0175f28f0';
const compactEmptyGuid = '00000000000000000000000000000000';

async function authenticate(request) {
    const response = await request.post('/Users/AuthenticateByName', {
        headers: {
            Authorization:
                'MediaBrowser Client="Collection Tag Sync Browser Tests", '
                + 'Device="Docker", DeviceId="collection-tag-sync-browser-tests", Version="0.1.0.0"'
        },
        data: {
            Username: process.env.JFTS_ADMIN_NAME,
            Pw: process.env.JFTS_ADMIN_PASSWORD
        }
    });
    expect(response.ok()).toBeTruthy();
    return (await response.json()).AccessToken;
}

async function saveConfiguration(request, accessToken, configuration) {
    const response = await request.post('/CollectionTagSync/Configuration', {
        headers: { 'X-Emby-Token': accessToken },
        data: configuration
    });
    expect(response.ok()).toBeTruthy();
}

async function clearRunOnceGroups(request, accessToken) {
    const response = await request.get('/CollectionTagSync/RunOnce/Groups', {
        headers: { 'X-Emby-Token': accessToken }
    });
    expect(response.ok()).toBeTruthy();
    for (const group of await response.json()) {
        const deleted = await request.delete(`/CollectionTagSync/RunOnce/Groups/${group.Id}`, {
            headers: { 'X-Emby-Token': accessToken }
        });
        expect(deleted.ok()).toBeTruthy();
    }
}

async function saveRunOnceGroup(request, accessToken, group) {
    const response = await request.post('/CollectionTagSync/RunOnce/Groups', {
        headers: { 'X-Emby-Token': accessToken },
        data: group
    });
    expect(response.ok()).toBeTruthy();
    return (await response.json()).Group;
}

async function signIn(page) {
    await page.goto('/web/');
    const username = page.getByRole('textbox', { name: /^user$/i });
    await expect(username).toBeVisible();
    await username.fill(process.env.JFTS_ADMIN_NAME);
    await page.getByRole('textbox', { name: /password/i }).fill(process.env.JFTS_ADMIN_PASSWORD);
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByRole('button', { name: process.env.JFTS_ADMIN_NAME })).toBeVisible();
}

async function openPluginConfiguration(page) {
    await page.goto(`/web/#/dashboard/plugins/${pluginId.replaceAll('-', '')}?name=Collection%20Tag%20Sync`);
    const settings = page.locator('#addPluginPage').getByRole('link', { name: 'Settings', exact: true });
    await expect(settings).toBeVisible();
    await expect(settings).toHaveAttribute(
        'href',
        '#/configurationpage?name=Collection%20Tag%20Sync');
    await settings.click();
    await expect(page.locator('#collectionTagSyncPage')).toBeVisible();
}

async function resetPluginState(request) {
    const accessToken = await authenticate(request);
    await saveConfiguration(request, accessToken, { SchemaVersion: 1, MappingGroups: [] });
    await clearRunOnceGroups(request, accessToken);
}

test.beforeEach(async ({ request }) => resetPluginState(request));
test.afterEach(async ({ request }) => resetPluginState(request));

test('saved mappings round-trip through the real Jellyfin administrator page', async ({ page, request }) => {
    const accessToken = await authenticate(request);
    await saveConfiguration(request, accessToken, {
        SchemaVersion: 1,
        MappingGroups: [{
            Target: {
                Kind: 'Tag',
                TagValue: 'Browser Target',
                CollectionId: compactEmptyGuid,
                CollectionDisplayName: ''
            },
            Sources: [{
                Kind: 'Tag',
                TagValue: 'Browser Source',
                CollectionId: compactEmptyGuid,
                CollectionDisplayName: ''
            }],
            Policy: 'Authoritative',
            IsEnabled: false
        }]
    });

    await signIn(page);
    await openPluginConfiguration(page);

    const mapping = page.locator('[data-mapping-group]').first();
    await expect(mapping.locator('[data-role="mapping-summary-flow"]'))
        .toHaveText('Tag “Browser Source” → Tag “Browser Target”');
    await expect(mapping.locator('[data-role="mapping-summary-policy"]')).toHaveText('Authoritative');
    await mapping.locator('summary').click();

    const target = mapping.locator('[data-role="target"] [data-node-editor]');
    await expect(target.locator('[data-field="node-kind"]')).toHaveValue('0');
    await expect(target.locator('[data-node-kind="tag"]')).toBeVisible();
    await expect(target.locator('[data-node-kind="collection"]')).toBeHidden();
    await expect(target.getByText(/Missing collection.*00000000000000000000000000000000/))
        .toHaveCount(0);

    await page.getByRole('button', { name: 'Save configuration' }).click();
    await expect(page.locator('#collectionTagSyncConfigurationStatus'))
        .toContainText(/configuration saved/i);

    const persistedResponse = await request.get(`/Plugins/${pluginId}/Configuration`, {
        headers: { 'X-Emby-Token': accessToken }
    });
    expect(persistedResponse.ok()).toBeTruthy();
    const persisted = await persistedResponse.json();
    expect(persisted.MappingGroups).toHaveLength(1);
    expect(persisted.MappingGroups[0]).toMatchObject({
        Target: { Kind: 'Tag', TagValue: 'Browser Target' },
        Sources: [{ Kind: 'Tag', TagValue: 'Browser Source' }],
        Policy: 'Authoritative',
        IsEnabled: false
    });
});

test('saved run-once groups are compact, persistent, and execute independently', async ({ page, request }) => {
    const accessToken = await authenticate(request);
    await clearRunOnceGroups(request, accessToken);
    const first = await saveRunOnceGroup(request, accessToken, {
        Target: { Kind: 'Tag', TagValue: 'Browser Run Target A' },
        Sources: [{ Kind: 'Tag', TagValue: 'Browser Run Source A' }],
        Policy: 'Additive'
    });
    const second = await saveRunOnceGroup(request, accessToken, {
        Target: { Kind: 'Tag', TagValue: 'Browser Run Target B' },
        Sources: [{ Kind: 'Tag', TagValue: 'Browser Run Source B' }],
        Policy: 'Authoritative'
    });

    await signIn(page);
    await openPluginConfiguration(page);

    const groups = page.locator('[data-run-once-group]');
    await expect(groups).toHaveCount(2);
    await expect(groups.nth(0)).not.toHaveAttribute('open', '');
    await expect(groups.nth(1)).not.toHaveAttribute('open', '');
    await expect(groups.nth(0).locator('[data-role="run-once-summary-flow"]'))
        .toHaveText('Tag “Browser Run Source A” → Tag “Browser Run Target A”');
    await expect(groups.nth(1).locator('[data-role="run-once-summary-flow"]'))
        .toHaveText('Tag “Browser Run Source B” → Tag “Browser Run Target B”');
    await expect(groups.nth(1).locator('[data-role="run-once-summary-policy"]'))
        .toHaveText('Authoritative');
    await expect(groups.nth(1).locator('[data-role="run-once-summary-state"]'))
        .toHaveText('Saved');

    await groups.nth(1).locator('summary').click();
    await expect(groups.nth(1)).toHaveAttribute('open', '');
    await expect(groups.nth(0)).not.toHaveAttribute('open', '');
    const secondTarget = groups.nth(1).locator(
        '[data-role="run-once-target"] [data-field="tag-value"]');
    await secondTarget.fill('Browser Run Target B Edited');
    await expect(groups.nth(1).locator('[data-role="run-once-summary-state"]'))
        .toHaveText('Unsaved');
    await groups.nth(1).getByRole('button', { name: 'Save group' }).click();
    await expect(page.locator('#collectionTagSyncRunOnceStatus')).toContainText(/group saved/i);

    await page.reload();
    await expect(page.locator('#collectionTagSyncPage')).toBeVisible();
    await expect(page.locator(`[data-run-once-group][data-group-id="${second.Id}"]`)
        .locator('[data-role="run-once-summary-flow"]'))
        .toHaveText('Tag “Browser Run Source B” → Tag “Browser Run Target B Edited”');

    const selected = page.locator(`[data-run-once-group][data-group-id="${second.Id}"]`);
    await selected.locator('summary').click();
    const previewRequest = page.waitForRequest(request =>
        request.url().endsWith('/CollectionTagSync/RunOnce/Preview')
        && request.method() === 'POST');
    await selected.getByRole('button', { name: 'Preview group' }).click();
    const previewBody = (await previewRequest).postDataJSON();
    expect(previewBody).toEqual({ GroupId: second.Id, ExcludedItemIds: [] });
    expect(JSON.stringify(previewBody)).not.toContain(first.Id);
    await expect(page.locator('#collectionTagSyncRunOnceStatus')).toContainText(/preview ready/i);

    await page.getByRole('button', { name: 'Run previewed group' }).click();
    await expect(page.locator('#collectionTagSyncRunOnceStatus')).toContainText(/execution queued/i);

    const persisted = await request.get('/CollectionTagSync/RunOnce/Groups', {
        headers: { 'X-Emby-Token': accessToken }
    });
    expect(persisted.ok()).toBeTruthy();
    const savedGroups = await persisted.json();
    expect(savedGroups).toHaveLength(2);
    expect(savedGroups.find(group => group.Id === second.Id)).toMatchObject({
        Target: { Kind: 'Tag', TagValue: 'Browser Run Target B Edited' },
        Sources: [{ Kind: 'Tag', TagValue: 'Browser Run Source B' }],
        Policy: 'Authoritative'
    });
    expect(savedGroups.some(group => 'ExcludedItemIds' in group)).toBeFalsy();

    const deleted = await request.delete(`/CollectionTagSync/RunOnce/Groups/${first.Id}`, {
        headers: { 'X-Emby-Token': accessToken }
    });
    expect(deleted.ok()).toBeTruthy();
    await page.reload();
    await expect(page.locator('[data-run-once-group]')).toHaveCount(1);
    await expect(page.locator(`[data-run-once-group][data-group-id="${second.Id}"]`)).toBeVisible();
});
