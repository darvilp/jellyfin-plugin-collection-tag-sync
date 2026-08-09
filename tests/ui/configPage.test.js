import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

import createPageController from '../../Jellyfin.Plugin.CollectionTagSync/Configuration/configPage.js';

const directItemId = '11111111-1111-1111-1111-111111111111';
const cascadeItemId = '22222222-2222-2222-2222-222222222222';
const emptyGuid = '00000000-0000-0000-0000-000000000000';

class FakeClassList {
    values = new Set();

    add(...values) {
        values.forEach(value => this.values.add(value));
    }

    remove(...values) {
        values.forEach(value => this.values.delete(value));
    }
}

class FakeElement {
    constructor({ value = '', checked = false, dataset = {}, selectors = [], closest = {} } = {}) {
        this.value = value;
        this.checked = checked;
        this.dataset = { ...dataset };
        this.selectors = new Set(selectors);
        this.closestValues = closest;
        this.classList = new FakeClassList();
        this.hidden = false;
        this.innerHTML = '';
        this.textContent = '';
    }

    matches(selector) {
        return this.selectors.has(selector);
    }

    closest(selector) {
        if (selector === 'button[data-action]' && this.dataset.action) {
            return this;
        }

        return this.closestValues[selector] ?? null;
    }

    focus() {
    }

    querySelectorAll() {
        return [];
    }

    insertAdjacentHTML(_position, html) {
        this.innerHTML += html;
    }

    remove() {
        this.removed = true;
    }
}

class FakeNodeEditor extends FakeElement {
    constructor(tagValue) {
        super({ closest: { '#collectionTagSyncRunOnce': {} } });
        this.kind = new FakeElement({ value: '0', selectors: ['[data-field="node-kind"]'] });
        this.tag = new FakeElement({
            value: tagValue,
            selectors: ['[data-field="tag-value"]'],
            closest: { '#collectionTagSyncRunOnce': {} }
        });
        this.collection = new FakeElement({ value: '', selectors: ['[data-field="collection-id"]'] });
    }

    querySelector(selector) {
        if (selector === '[data-field="node-kind"]') {
            return this.kind;
        }

        if (selector === '[data-field="tag-value"]') {
            return this.tag;
        }

        if (selector === '[data-field="collection-id"]') {
            return this.collection;
        }

        return new FakeElement();
    }
}

class FakeView {
    constructor() {
        this.listeners = new Map();
        this.elements = new Map();
        this.targetEditor = new FakeNodeEditor('Kid-Approved');
        this.sourceEditor = new FakeNodeEditor('Waltney');
        this.elements.set('#collectionTagSyncRunOncePolicy', new FakeElement({ value: '0' }));
    }

    addEventListener(type, listener) {
        this.listeners.set(type, listener);
    }

    contains() {
        return true;
    }

    querySelector(selector) {
        if (selector === '#collectionTagSyncRunOnceTarget [data-node-editor]') {
            return this.targetEditor;
        }

        if (!this.elements.has(selector)) {
            this.elements.set(selector, new FakeElement());
        }

        return this.elements.get(selector);
    }

    querySelectorAll(selector) {
        if (selector === '#collectionTagSyncRunOnceSources [data-node-editor]') {
            return [this.sourceEditor];
        }

        return [];
    }

    async dispatch(type, target = new FakeElement()) {
        const listener = this.listeners.get(type);
        assert.ok(listener, `Expected a ${type} listener.`);
        await listener({ target });
    }
}

function button(action) {
    return new FakeElement({ dataset: { action } });
}

function exclusion(itemId, checked) {
    return new FakeElement({
        checked,
        dataset: { runOnceExclusion: itemId },
        selectors: ['[data-run-once-exclusion]'],
        closest: { '#collectionTagSyncRunOnce': {} }
    });
}

function runOncePreview({
    validationErrors = [],
    excludableItemIds = [],
    items = []
} = {}) {
    return {
        Outcome: validationErrors.length === 0 ? 'Ready' : 'Invalid',
        Authorization: validationErrors.length === 0 ? {
            Authorization: 'opaque-run-once-authorization',
            ExcludableItemIds: excludableItemIds,
            ExpiresAtUtc: '2026-08-09T20:00:00Z',
            Preview: { TotalItemCount: items.length, Items: items }
        } : null,
        ValidationErrors: validationErrors
    };
}

function createHarness(responder = async () => ({}), configuration = { MappingGroups: [] }) {
    const calls = [];
    const view = new FakeView();
    const apiClient = {
        getUrl: path => path,
        getPluginConfiguration: async () => configuration,
        ajax: async options => {
            const body = options.data ? JSON.parse(options.data) : undefined;
            calls.push({ method: options.type, path: options.url, body });
            return responder(options.url, options.type, body);
        }
    };
    const scheduled = [];
    globalThis.window = {
        ApiClient: apiClient,
        Dashboard: { showLoadingMsg() {}, hideLoadingMsg() {} },
        setTimeout(action) {
            scheduled.push(action);
            return scheduled.length;
        },
        clearTimeout() {}
    };
    createPageController(view);
    return { calls, scheduled, view };
}

test('rendered collection picker keeps duplicate names as distinct GUID choices', async () => {
    const configuration = {
        SchemaVersion: 1,
        MappingGroups: [{
            Target: { Kind: 1, CollectionId: directItemId, CollectionDisplayName: 'Animation' },
            Sources: [{ Kind: 0, TagValue: 'Waltney' }],
            Policy: 0,
            IsEnabled: true
        }]
    };
    const { view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Collections/Picker') {
            return [
                { Id: directItemId, DisplayName: 'Animation' },
                { Id: cascadeItemId, DisplayName: 'Animation' }
            ];
        }

        if (path === 'CollectionTagSync/Tags/Picker') {
            return ['Waltney'];
        }

        if (path === 'CollectionTagSync/FullReconcile/Status') {
            return { Id: '00000000-0000-0000-0000-000000000000', State: 'Idle' };
        }

        if (path === 'CollectionTagSync/Status') {
            return {
                Incremental: {},
                FullReconcileRequest: { Reasons: [] },
                UnresolvedGroups: []
            };
        }

        return {};
    }, configuration);

    await view.dispatch('viewshow');

    const rendered = view.querySelector('#collectionTagSyncMappingGroups').innerHTML;
    assert.match(rendered, new RegExp(`value="${directItemId}"`));
    assert.match(rendered, new RegExp(`value="${cascadeItemId}"`));
    assert.match(rendered, new RegExp(`Animation — ${directItemId}`));
    assert.match(rendered, new RegExp(`Animation — ${cascadeItemId}`));
});

test('fresh collection nodes render the empty picker prompt instead of an unresolved zero GUID', async () => {
    const { view } = createHarness();

    await view.dispatch('click', button('add-mapping'));

    const rendered = view.querySelector('#collectionTagSyncMappingGroups').innerHTML;
    assert.match(rendered, /<option value="">Select a collection…<\/option>/);
    assert.doesNotMatch(rendered, new RegExp(`Missing collection.*${emptyGuid}`));
});

test('rendered validation shows the server message without client-side rule substitution', async () => {
    const serverMessage = 'The server says the enabled graph contains a cycle.';
    const { view } = createHarness(async path => {
        if (path === 'CollectionTagSync/RunOnce/Preview') {
            throw { json: async () => runOncePreview({
                validationErrors: [{ Code: 3, Message: serverMessage }]
            }) };
        }

        return {};
    });

    await view.dispatch('click', button('preview-run-once'));

    assert.equal(view.querySelector('#collectionTagSyncRunOnceStatus').textContent, serverMessage);
});

test('editor input invalidates a rendered preview and blocks stale confirmation', async () => {
    const { calls, view } = createHarness(async path => {
        if (path === 'CollectionTagSync/RunOnce/Preview') {
            return runOncePreview();
        }

        return {};
    });
    await view.dispatch('click', button('preview-run-once'));

    await view.dispatch('input', view.sourceEditor.tag);
    await view.dispatch('click', button('confirm-run-once'));

    assert.equal(calls.filter(call => call.path.endsWith('/Confirm')).length, 0);
    assert.match(view.querySelector('#collectionTagSyncRunOnceStatus').textContent, /preview is stale/i);
});

test('two exclusion checkbox input/change sequences retain both IDs in the next request', async () => {
    const { calls, view } = createHarness(async path => {
        if (path === 'CollectionTagSync/RunOnce/Preview') {
            return runOncePreview({ excludableItemIds: [directItemId, cascadeItemId] });
        }

        return {};
    });

    for (const itemId of [directItemId, cascadeItemId]) {
        const checkbox = exclusion(itemId, true);
        await view.dispatch('input', checkbox);
        await view.dispatch('change', checkbox);
    }
    await view.dispatch('click', button('preview-run-once'));

    const request = calls.find(call => call.path === 'CollectionTagSync/RunOnce/Preview');
    assert.deepEqual(request.body.ExcludedItemIds, [directItemId, cascadeItemId]);
});

test('run-once preview renders exclusions only for server-marked direct target changes', async () => {
    const items = [
        {
            ItemId: directItemId,
            ItemKind: 'Movie',
            Mutations: [{
                Kind: 'AddCollectionMembership',
                Target: { Kind: 1, CollectionId: directItemId, CollectionDisplayName: 'Animation' }
            }]
        },
        {
            ItemId: cascadeItemId,
            ItemKind: 'Series',
            Mutations: [{ Kind: 'AddTag', Target: { Kind: 0, TagValue: 'animated' } }]
        }
    ];
    const { view } = createHarness(async path =>
        path === 'CollectionTagSync/RunOnce/Preview'
            ? runOncePreview({ excludableItemIds: [directItemId], items })
            : {});

    await view.dispatch('click', button('preview-run-once'));

    const rendered = view.querySelector('#collectionTagSyncRunOncePreview').innerHTML;
    assert.match(rendered, new RegExp(`data-run-once-exclusion="${directItemId}"`));
    assert.doesNotMatch(rendered, new RegExp(`data-run-once-exclusion="${cascadeItemId}"`));
    assert.match(rendered, /Add Collection/);
    assert.match(rendered, /Add Tag/);
});

test('background status is rendered through the controller for every lifecycle state', async () => {
    const expected = new Map([
        [0, 'Queued'],
        [1, 'Running'],
        [2, 'Completed'],
        [3, 'Completed with failures'],
        [4, 'Failed'],
        [5, 'Paused for approval']
    ]);

    for (const [state, label] of expected) {
        const { view } = createHarness(async (path, method) => {
            if (path === 'CollectionTagSync/RunOnce/Preview') {
                return runOncePreview();
            }

            if (path === 'CollectionTagSync/RunOnce/Confirm') {
                return { Outcome: 'Accepted', ReconciliationId: directItemId };
            }

            if (method === 'GET' && path.includes('/Reconciliations/')) {
                return {
                    State: state,
                    CompletedItemCount: 2,
                    TotalItemCount: 4,
                    FailedItemCount: 1
                };
            }

            return {};
        });
        await view.dispatch('click', button('preview-run-once'));
        await view.dispatch('click', button('confirm-run-once'));
        await Promise.resolve();

        assert.match(
            view.querySelector('#collectionTagSyncRunOnceReconciliation').textContent,
            new RegExp(`^${label}`));
    }
});

test('paused Full Reconcile renders every server-provided item removal before confirmation', async () => {
    const { view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Collections/Picker' || path === 'CollectionTagSync/Tags/Picker') {
            return [];
        }

        if (path === 'CollectionTagSync/FullReconcile/Status') {
            return { Id: directItemId, State: 'AwaitingApproval' };
        }

        if (path.endsWith('/Preview')) {
            return {
                Authorization: 'opaque-full-reconcile-authorization',
                Preview: {
                    UniqueAffectedItemCount: 2,
                    ExceedsAbsoluteLimit: true,
                    Removals: [
                        {
                            ItemId: directItemId,
                            Kind: 'RemoveTag',
                            Target: { Kind: 0, TagValue: 'Kid-Approved' }
                        },
                        {
                            ItemId: cascadeItemId,
                            Kind: 'RemoveCollectionMembership',
                            Target: { Kind: 1, CollectionId: cascadeItemId, CollectionDisplayName: 'Animation' }
                        }
                    ],
                    Groups: []
                }
            };
        }

        if (path === 'CollectionTagSync/Status') {
            return {
                Incremental: {},
                FullReconcileRequest: { Reasons: [] },
                UnresolvedGroups: []
            };
        }

        return {};
    });

    await view.dispatch('viewshow');

    const rendered = view.querySelector('#collectionTagSyncFullReconcilePreview').innerHTML;
    assert.match(rendered, new RegExp(directItemId));
    assert.match(rendered, /Remove\s+Tag &quot;Kid-Approved&quot;/);
    assert.match(rendered, new RegExp(cascadeItemId));
    assert.match(rendered, /Remove\s+Collection &quot;Animation&quot;/);
    assert.equal(view.querySelector('[data-action="confirm-full-reconcile"]').hidden, false);
});

test('all UI actions are native keyboard-operable controls with announced status', async () => {
    const html = await readFile(new URL(
        '../../Jellyfin.Plugin.CollectionTagSync/Configuration/configPage.html',
        import.meta.url), 'utf8');

    const actionTags = [...html.matchAll(/<(\w+)[^>]*data-action=/g)].map(match => match[1]);
    assert.ok(actionTags.length > 0);
    assert.ok(actionTags.every(tag => tag === 'button'));
    assert.match(html, /aria-live="polite"/);
    assert.match(html, /<label[^>]+for="collectionTagSyncNewCollectionName"/);
});
