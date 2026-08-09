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
        this.open = false;
        this.innerHTML = '';
        this.textContent = '';
        this.listeners = new Map();
    }

    addEventListener(type, listener) {
        this.listeners.set(type, listener);
    }

    async dispatch(type, details = {}) {
        const listener = this.listeners.get(type);
        assert.ok(listener, `Expected a ${type} listener.`);
        await listener({ target: this, preventDefault() {}, ...details });
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
        this.focused = true;
    }

    showModal() {
        this.open = true;
    }

    close() {
        this.open = false;
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
        this.mappingGroups = [];
        this.targetEditor = new FakeNodeEditor('Kid-Approved');
        this.sourceEditor = new FakeNodeEditor('Waltney');
        this.elements.set('#collectionTagSyncRunOncePolicy', new FakeElement({ value: '0' }));
        this.elements.set('[data-action="save-configuration"]', new FakeElement());
        const confirmConfiguration = new FakeElement();
        confirmConfiguration.hidden = true;
        this.elements.set('[data-action="confirm-configuration"]', confirmConfiguration);
        const configurationPreview = new FakeElement();
        configurationPreview.hidden = true;
        this.elements.set('#collectionTagSyncConfigurationPreview', configurationPreview);
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
        if (selector === '[data-mapping-group]') {
            return this.mappingGroups;
        }

        if (selector === '#collectionTagSyncRunOnceSources [data-node-editor]') {
            return [this.sourceEditor];
        }

        return [];
    }

    async dispatch(type, target = new FakeElement(), details = {}) {
        const listener = this.listeners.get(type);
        assert.ok(listener, `Expected a ${type} listener.`);
        await listener({ target, preventDefault() {}, ...details });
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

function collectionPicker(value = directItemId) {
    const editor = new FakeElement({ dataset: { collectionDisplayName: 'Animation' } });
    return new FakeElement({
        value: '__add_new_collection__',
        dataset: { previousValue: value },
        selectors: ['[data-field="collection-id"]'],
        closest: { '[data-node-editor]': editor }
    });
}

function editableMappingGroup({ collectionTarget = false } = {}) {
    const group = new FakeElement();
    const target = new FakeNodeEditor('Kid-Approved');
    const source = new FakeNodeEditor('Waltney');
    const sources = [source];
    const sourcesContainer = new FakeElement();
    sourcesContainer.querySelectorAll = selector => selector === '[data-node-editor]' ? sources : [];
    sourcesContainer.insertAdjacentHTML = () => {
        sources.push(new FakeNodeEditor(''));
    };
    const policy = new FakeElement({ value: '0' });
    const enabled = new FakeElement({ checked: true });
    const flow = new FakeElement();
    const policySummary = new FakeElement();
    const stateSummary = new FakeElement();
    const mappingClosest = {
        '[data-mapping-group]': group,
        '#collectionTagSyncMappings': {}
    };
    source.tag.closestValues = mappingClosest;
    source.remove = () => sources.splice(sources.indexOf(source), 1);
    policy.closestValues = mappingClosest;
    enabled.closestValues = mappingClosest;
    group.closestValues['[data-mapping-group]'] = group;
    if (collectionTarget) {
        target.kind.value = '1';
        target.dataset.collectionDisplayName = 'Animation';
        target.collection.value = directItemId;
        target.collection.dataset.previousValue = directItemId;
        target.collection.closestValues = {
            '[data-node-editor]': target,
            ...mappingClosest
        };
    }

    group.querySelector = selector => ({
        '[data-role="target"] [data-node-editor]': target,
        '[data-role="sources"]': sourcesContainer,
        '[data-field="policy"]': policy,
        '[data-field="enabled"]': enabled,
        '[data-role="mapping-summary-flow"]': flow,
        '[data-role="mapping-summary-policy"]': policySummary,
        '[data-role="mapping-summary-state"]': stateSummary
    })[selector] ?? new FakeElement();
    group.querySelectorAll = selector => selector === '[data-role="sources"] [data-node-editor]'
        ? sources
        : [];
    return { enabled, flow, group, policy, policySummary, source, sources, stateSummary, target };
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

function configurationPreview({ removals = 0 } = {}) {
    return {
        Outcome: 'Ready',
        Authorization: {
            Authorization: 'opaque-configuration-authorization',
            ExpiresAtUtc: '2026-08-09T20:00:00Z',
            Preview: {
                TotalItemCount: 1,
                Items: [{
                    ItemId: directItemId,
                    ItemTitle: 'Waltney Adventure',
                    ItemKind: 'Movie',
                    Mutations: removals > 0
                        ? [{ Kind: 'RemoveTag', Target: { Kind: 0, TagValue: 'Kid-Approved' } }]
                        : [{ Kind: 'AddTag', Target: { Kind: 0, TagValue: 'Kid-Approved' } }]
                }]
            }
        },
        ValidationErrors: []
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

test('configured mappings render as collapsed source-to-target summaries with one Edit disclosure group', async () => {
    const configuration = {
        SchemaVersion: 1,
        MappingGroups: [{
            Target: { Kind: 1, CollectionId: directItemId, CollectionDisplayName: 'Waltney Picks' },
            Sources: [
                { Kind: 0, TagValue: 'Waltney' },
                { Kind: 1, CollectionId: cascadeItemId, CollectionDisplayName: 'Blooth Archive' }
            ],
            Policy: 1,
            IsEnabled: true
        }, {
            Target: { Kind: 0, TagValue: 'Kid-Approved' },
            Sources: [{ Kind: 0, TagValue: 'Family Night' }],
            Policy: 0,
            IsEnabled: false
        }]
    };
    const { view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Collections/Picker') {
            return [
                { Id: directItemId, DisplayName: 'Waltney Picks' },
                { Id: cascadeItemId, DisplayName: 'Blooth Archive' }
            ];
        }

        if (path === 'CollectionTagSync/Tags/Picker') {
            return ['Waltney', 'Family Night', 'Kid-Approved'];
        }

        return {};
    }, configuration);

    await view.dispatch('viewshow');

    const rendered = view.querySelector('#collectionTagSyncMappingGroups').innerHTML;
    assert.equal([...rendered.matchAll(/data-mapping-group/g)].length, 2);
    assert.equal([...rendered.matchAll(/name="collectionTagSyncMappingEditors"/g)].length, 2);
    assert.doesNotMatch(rendered, /<details[^>]*\sopen(?:\s|>)/);
    assert.match(
        rendered,
        /Tag “Waltney” OR Collection “Blooth Archive” → Collection “Waltney Picks”/);
    assert.match(rendered, /Authoritative/);
    assert.match(rendered, /Enabled/);
    assert.match(rendered, /<summary[^>]*>[\s\S]*Edit[\s\S]*<\/summary>/);
    assert.match(rendered, /data-role="mapping-editor"/);
});

test('collapsed summaries identify unresolved source and target collection GUIDs', async () => {
    const configuration = {
        SchemaVersion: 1,
        MappingGroups: [{
            Target: { Kind: 1, CollectionId: directItemId, CollectionDisplayName: 'Former target' },
            Sources: [{ Kind: 1, CollectionId: cascadeItemId, CollectionDisplayName: 'Former source' }],
            Policy: 1,
            IsEnabled: true
        }]
    };
    const { view } = createHarness(async path =>
        path.endsWith('/Picker') ? [] : {}, configuration);

    await view.dispatch('viewshow');

    const rendered = view.querySelector('#collectionTagSyncMappingGroups').innerHTML;
    assert.match(rendered, new RegExp(`Missing collection “Former source” — ${cascadeItemId}`));
    assert.match(rendered, new RegExp(`→ Missing collection “Former target” — ${directItemId}`));
    assert.doesNotMatch(rendered, /Collection “Former (?:source|target)”/);
});

test('a newly added mapping is the only expanded continuous editor', async () => {
    const { view } = createHarness();

    await view.dispatch('click', button('add-mapping'));

    const rendered = view.querySelector('#collectionTagSyncMappingGroups').innerHTML;
    assert.equal([...rendered.matchAll(/<details[^>]*\sopen(?:\s|>)/g)].length, 1);
    assert.match(rendered, /<summary[^>]*>[\s\S]*Edit[\s\S]*<\/summary>/);
    assert.match(rendered, /Edit mapping group 1/);
});

test('adding a mapping preserves unsaved existing editors and opens only the new group', async () => {
    const { view } = createHarness();
    const existing = editableMappingGroup();
    existing.source.tag.value = 'Unsaved Waltney';
    existing.policy.value = '1';
    existing.enabled.checked = false;
    view.mappingGroups = [existing.group];

    await view.dispatch('click', button('add-mapping'));

    const rendered = view.querySelector('#collectionTagSyncMappingGroups').innerHTML;
    assert.equal([...rendered.matchAll(/data-mapping-group/g)].length, 2);
    assert.equal([...rendered.matchAll(/<details[^>]*\sopen(?:\s|>)/g)].length, 1);
    assert.match(rendered, /Tag “Unsaved Waltney” → Tag “Kid-Approved”/);
    assert.match(rendered, /Authoritative/);
    assert.match(rendered, /Disabled/);
    assert.match(rendered, /Edit mapping group 2/);
});

test('Edit disclosures close the prior mapping and toggle the selected editor', async () => {
    const { view } = createHarness();
    const first = new FakeElement();
    first.open = true;
    const second = new FakeElement();
    const summary = new FakeElement();
    summary.closestValues['summary[data-action="edit-mapping"]'] = summary;
    summary.closestValues['[data-mapping-group]'] = second;
    view.mappingGroups = [first, second];

    await view.dispatch('click', summary);
    assert.equal(first.open, false);
    assert.equal(second.open, true);

    await view.dispatch('click', summary);
    assert.equal(second.open, false);
});

test('editing a mapping refreshes its compact flow, policy, and enabled summary', async () => {
    const { view } = createHarness();
    const mapping = editableMappingGroup();
    mapping.source.tag.value = 'Blooth';

    await view.dispatch('input', mapping.source.tag);
    assert.equal(mapping.flow.textContent, 'Tag “Blooth” → Tag “Kid-Approved”');

    mapping.policy.value = '1';
    await view.dispatch('change', mapping.policy);
    assert.equal(mapping.policySummary.textContent, 'Authoritative');

    mapping.enabled.checked = false;
    await view.dispatch('change', mapping.enabled);
    assert.equal(mapping.stateSummary.textContent, 'Disabled');
});

test('adding and removing sources refreshes the compact OR summary', async () => {
    const { view } = createHarness();
    const mapping = editableMappingGroup();
    view.mappingGroups = [mapping.group];
    const addButton = button('add-mapping-source');
    addButton.closestValues['[data-mapping-group]'] = mapping.group;

    await view.dispatch('click', addButton);
    assert.equal(
        mapping.flow.textContent,
        'Tag “Waltney” OR Tag (not selected) → Tag “Kid-Approved”');

    const removeButton = button('remove-node');
    removeButton.closestValues['[data-mapping-group]'] = mapping.group;
    removeButton.closestValues['[data-node-editor]'] = mapping.source;
    await view.dispatch('click', removeButton);
    assert.equal(mapping.flow.textContent, 'Tag (not selected) → Tag “Kid-Approved”');
});

test('collection creation refreshes a compact target summary with the returned display name', async () => {
    const mapping = editableMappingGroup({ collectionTarget: true });
    const { view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Collections/Create') {
            return {
                Outcome: 'Created',
                SelectedCollection: { Id: cascadeItemId, DisplayName: 'Waltney Picks' }
            };
        }

        return {};
    });
    view.mappingGroups = [mapping.group];
    mapping.target.collection.value = '__add_new_collection__';

    await view.dispatch('change', mapping.target.collection);
    view.querySelector('#collectionTagSyncNewCollectionName').value = 'Waltney Picks';
    await view.dispatch('click', button('create-collection'));

    assert.equal(mapping.target.collection.value, cascadeItemId);
    assert.equal(mapping.flow.textContent, 'Tag “Waltney” → Collection “Waltney Picks”');
});

test('collection creation is a native modal associated with the originating picker', async () => {
    const html = await readFile(new URL(
        '../../Jellyfin.Plugin.CollectionTagSync/Configuration/configPage.html',
        import.meta.url), 'utf8');

    assert.match(html, /<dialog[^>]+id="collectionTagSyncCreateCollection"/);
    assert.match(html, /aria-modal="true"/);
    assert.match(html, /aria-describedby="collectionTagSyncCreateCollectionDescription"/);
    assert.match(html, /selected in the picker you opened this from/i);
    assert.match(html, /starts locked so metadata providers cannot replace its explicit name/i);
    assert.match(html, /unlock it later in\s+Jellyfin/i);
    assert.match(html, /\.collectionTagSyncDialog::backdrop/);
    assert.doesNotMatch(html, /<section[^>]+id="collectionTagSyncCreateCollection"/);
});

test('canceling collection modal returns focus to the originating picker', async () => {
    const { view } = createHarness();
    const picker = collectionPicker();

    await view.dispatch('change', picker);

    assert.equal(view.querySelector('#collectionTagSyncCreateCollection').open, true);
    assert.equal(view.querySelector('#collectionTagSyncNewCollectionName').focused, true);
    await view.dispatch('click', button('cancel-create-collection'));
    assert.equal(view.querySelector('#collectionTagSyncCreateCollection').open, false);
    assert.equal(picker.focused, true);
});

test('native dialog cancellation closes the modal and returns focus to its picker', async () => {
    const { view } = createHarness();
    const picker = collectionPicker();
    await view.dispatch('change', picker);
    let prevented = false;

    await view.querySelector('#collectionTagSyncCreateCollection').dispatch('cancel', {
        preventDefault() {
            prevented = true;
        }
    });

    assert.equal(prevented, true);
    assert.equal(view.querySelector('#collectionTagSyncCreateCollection').open, false);
    assert.equal(picker.focused, true);
});

test('created collection is selected by GUID before modal focus returns', async () => {
    const picker = collectionPicker();
    const { calls, view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Collections/Create') {
            return {
                Outcome: 'Created',
                SelectedCollection: { Id: cascadeItemId, DisplayName: 'Waltney Picks' }
            };
        }

        return {};
    });
    await view.dispatch('change', picker);
    view.querySelector('#collectionTagSyncNewCollectionName').value = 'Waltney Picks';

    await view.dispatch('click', button('create-collection'));

    const request = calls.find(call => call.path === 'CollectionTagSync/Collections/Create');
    assert.deepEqual(request.body, { Name: 'Waltney Picks' });
    assert.equal(picker.value, cascadeItemId);
    assert.equal(picker.closest('[data-node-editor]').dataset.collectionDisplayName, 'Waltney Picks');
    assert.equal(view.querySelector('#collectionTagSyncCreateCollection').open, false);
    assert.equal(picker.focused, true);
});

test('late collection creation cannot act on a reopened modal or duplicate its request', async () => {
    const firstCreatedId = '33333333-3333-3333-3333-333333333333';
    const secondCreatedId = '44444444-4444-4444-4444-444444444444';
    let finishFirst;
    let finishSecond;
    const firstResponse = new Promise(resolve => {
        finishFirst = resolve;
    });
    const secondResponse = new Promise(resolve => {
        finishSecond = resolve;
    });
    let createCalls = 0;
    const { calls, view } = createHarness(async path => {
        if (path !== 'CollectionTagSync/Collections/Create') {
            return {};
        }

        createCalls += 1;
        return createCalls === 1 ? firstResponse : secondResponse;
    });
    const firstPicker = collectionPicker();
    const secondPicker = collectionPicker(cascadeItemId);

    await view.dispatch('change', firstPicker);
    view.querySelector('#collectionTagSyncNewCollectionName').value = 'First collection';
    const firstRequest = view.dispatch('click', button('create-collection'));
    await Promise.resolve();
    assert.equal(view.querySelector('[data-action="create-collection"]').disabled, true);
    await view.dispatch('click', button('create-collection'));
    assert.equal(calls.filter(call => call.path === 'CollectionTagSync/Collections/Create').length, 1);

    await view.dispatch('click', button('cancel-create-collection'));
    await view.dispatch('change', secondPicker);
    view.querySelector('#collectionTagSyncNewCollectionName').value = 'Second collection';
    const secondRequest = view.dispatch('click', button('create-collection'));
    await Promise.resolve();

    finishFirst({
        Outcome: 'Created',
        SelectedCollection: { Id: firstCreatedId, DisplayName: 'First collection' }
    });
    await firstRequest;
    assert.equal(view.querySelector('#collectionTagSyncCreateCollection').open, true);
    assert.notEqual(secondPicker.value, firstCreatedId);
    assert.equal(secondPicker.closest('[data-node-editor]').dataset.collectionDisplayName, 'Animation');

    finishSecond({
        Outcome: 'Created',
        SelectedCollection: { Id: secondCreatedId, DisplayName: 'Second collection' }
    });
    await secondRequest;
    assert.equal(secondPicker.value, secondCreatedId);
    assert.equal(secondPicker.closest('[data-node-editor]').dataset.collectionDisplayName, 'Second collection');
    assert.equal(view.querySelector('#collectionTagSyncCreateCollection').open, false);
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

test('configuration actions explain the branching save and preview workflow', async () => {
    const html = await readFile(new URL(
        '../../Jellyfin.Plugin.CollectionTagSync/Configuration/configPage.html',
        import.meta.url), 'utf8');

    assert.match(html, /<span>Save configuration<\/span>/);
    assert.match(html, /<span>Preview changes<\/span>/);
    assert.match(html, /<span>Confirm removals and save<\/span>/);
    assert.match(html, /Preview changes does not save/i);
    assert.match(html, /metadata changes settle in the background/i);
    assert.match(html, /id="collectionTagSyncConfigurationPreview"[^>]+tabindex="-1"/);
    assert.doesNotMatch(html, /Validate and save|Preview candidate configuration|Confirm previewed configuration/);
});

test('global reconciliation and advanced safety settings precede continuous mappings', async () => {
    const html = await readFile(new URL(
        '../../Jellyfin.Plugin.CollectionTagSync/Configuration/configPage.html',
        import.meta.url), 'utf8');
    const safetyStart = html.indexOf('<section id="collectionTagSyncReconciliationSafety"');
    const mappingsStart = html.indexOf('<section id="collectionTagSyncMappings"');
    const safetyEnd = html.indexOf('</section>', safetyStart);
    const safety = html.slice(safetyStart, safetyEnd);
    const advancedStart = safety.indexOf('<details id="collectionTagSyncAdvancedSafety"');
    const advancedEnd = safety.indexOf('</details>', advancedStart);

    assert.ok(safetyStart > 0);
    assert.ok(safetyStart < mappingsStart);
    assert.match(safety, /<h2[^>]*>Reconciliation and safety<\/h2>/);
    assert.ok(safety.indexOf('id="collectionTagSyncStartupDelay"') < advancedStart);
    assert.ok(safety.indexOf('id="collectionTagSyncCircuitBreakerEnabled"') < advancedStart);
    assert.match(safety.slice(advancedStart, advancedEnd), /Advanced safety thresholds/);
    assert.match(safety.slice(advancedStart, advancedEnd), /id="collectionTagSyncMaximumItems"/);
    assert.match(safety.slice(advancedStart, advancedEnd), /id="collectionTagSyncMaximumPercentage"/);
    assert.match(safety.slice(advancedStart, advancedEnd), /id="collectionTagSyncMinimumPopulation"/);
    assert.ok(safety.indexOf('id="collectionTagSyncCircuitBreakerWarning"') > advancedEnd);
    assert.doesNotMatch(safety.slice(advancedStart, advancedEnd), /\sopen(?:\s|>)/);
    assert.doesNotMatch(html.slice(mappingsStart), /<h3>Reconciliation and safety<\/h3>/);
});

test('editing global reconciliation settings invalidates a destructive configuration preview', async () => {
    const { view } = createHarness(async path =>
        path === 'CollectionTagSync/Configuration/Preview'
            ? configurationPreview({ removals: 1 })
            : {});
    await view.dispatch('click', button('preview-configuration'));
    assert.equal(view.querySelector('#collectionTagSyncConfigurationPreview').hidden, false);

    const safetySetting = new FakeElement({
        closest: { '#collectionTagSyncReconciliationSafety': {} }
    });
    await view.dispatch('input', safetySetting);

    assert.equal(view.querySelector('#collectionTagSyncConfigurationPreview').hidden, true);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /configuration changed/i);
});

test('removal-free save reports configuration acceptance before background settlement', async () => {
    const { calls, view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Configuration') {
            return { Outcome: 'Accepted', ActiveRevision: 2, ReconciliationId: directItemId };
        }

        if (path.includes('/Reconciliations/')) {
            return { State: 'Queued', CompletedItemCount: 0, TotalItemCount: 1, FailedItemCount: 0 };
        }

        return {};
    });

    await view.dispatch('click', button('save-configuration'));

    assert.equal(calls.filter(call => call.path === 'CollectionTagSync/Configuration').length, 1);
    assert.equal(calls.filter(call => call.path.endsWith('/Preview')).length, 0);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /configuration saved/i);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /background/i);
    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
});

test('save that requires approval opens preview and replaces save with explicit confirmation', async () => {
    const { calls, view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Configuration') {
            throw { json: async () => ({ Outcome: 'RequiresPreview' }) };
        }

        if (path === 'CollectionTagSync/Configuration/Preview') {
            return configurationPreview({ removals: 1 });
        }

        if (path === 'CollectionTagSync/Configuration/Confirm') {
            return { Outcome: 'Accepted', ActiveRevision: 2, ReconciliationId: directItemId };
        }

        if (path.includes('/Reconciliations/')) {
            return { State: 'Queued', CompletedItemCount: 0, TotalItemCount: 1, FailedItemCount: 0 };
        }

        return {};
    });

    await view.dispatch('click', button('save-configuration'));

    assert.deepEqual(
        calls.slice(0, 2).map(call => call.path),
        ['CollectionTagSync/Configuration', 'CollectionTagSync/Configuration/Preview']);
    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, true);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, false);
    assert.equal(view.querySelector('#collectionTagSyncConfigurationPreview').focused, true);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /no changes.*saved/i);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /confirm removals and save/i);

    await view.dispatch('click', button('confirm-configuration'));

    assert.equal(calls.filter(call => call.path.endsWith('/Confirm')).length, 1);
    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /configuration saved/i);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /background/i);
});

test('optional removal-free preview saves nothing and retains normal save action', async () => {
    const { calls, view } = createHarness(async path =>
        path === 'CollectionTagSync/Configuration/Preview'
            ? configurationPreview()
            : {});

    await view.dispatch('click', button('preview-configuration'));

    assert.equal(calls.filter(call => call.path === 'CollectionTagSync/Configuration').length, 0);
    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /no changes.*saved/i);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /save configuration/i);
});

test('editing after a destructive preview restores save and removes stale confirmation', async () => {
    const { view } = createHarness(async path =>
        path === 'CollectionTagSync/Configuration/Preview'
            ? configurationPreview({ removals: 1 })
            : {});
    await view.dispatch('click', button('preview-configuration'));
    const editedSetting = new FakeElement({ closest: { '#collectionTagSyncMappings': {} } });

    await view.dispatch('input', editedSetting);

    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.equal(view.querySelector('#collectionTagSyncConfigurationPreview').hidden, true);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /configuration changed/i);
});

test('failed replacement preview removes the prior destructive authorization and preview', async () => {
    let previewCount = 0;
    const { view } = createHarness(async path => {
        if (path !== 'CollectionTagSync/Configuration/Preview') {
            return {};
        }

        previewCount++;
        if (previewCount === 1) {
            return configurationPreview({ removals: 1 });
        }

        throw { json: async () => ({ ValidationErrors: [{ Message: 'Candidate is no longer valid.' }] }) };
    });
    await view.dispatch('click', button('preview-configuration'));

    await view.dispatch('click', button('preview-configuration'));

    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.equal(view.querySelector('#collectionTagSyncConfigurationPreview').hidden, true);
    assert.equal(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, 'Candidate is no longer valid.');
});

test('replacement preview invalidates prior confirmation before the request completes', async () => {
    let previewCount = 0;
    let finishReplacement;
    const replacementResponse = new Promise(resolve => {
        finishReplacement = resolve;
    });
    const { calls, view } = createHarness(async path => {
        if (path !== 'CollectionTagSync/Configuration/Preview') {
            return {};
        }

        previewCount++;
        return previewCount === 1
            ? configurationPreview({ removals: 1 })
            : replacementResponse;
    });
    await view.dispatch('click', button('preview-configuration'));

    const replacement = view.dispatch('click', button('preview-configuration'));
    await Promise.resolve();
    await view.dispatch('click', button('confirm-configuration'));

    assert.equal(calls.filter(call => call.path.endsWith('/Confirm')).length, 0);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.equal(view.querySelector('#collectionTagSyncConfigurationPreview').hidden, true);
    finishReplacement(configurationPreview({ removals: 1 }));
    await replacement;
});

test('ordinary typing without an active preview does not rewrite the polite status region', async () => {
    const { view } = createHarness();
    const status = view.querySelector('#collectionTagSyncConfigurationStatus');
    status.textContent = 'Existing status.';
    const editedSetting = new FakeElement({ closest: { '#collectionTagSyncMappings': {} } });

    await view.dispatch('input', editedSetting);

    assert.equal(status.textContent, 'Existing status.');
});

test('editing while a preview request is pending ignores its late response', async () => {
    let finishPreview;
    const previewResponse = new Promise(resolve => {
        finishPreview = resolve;
    });
    const { view } = createHarness(async path =>
        path === 'CollectionTagSync/Configuration/Preview' ? previewResponse : {});

    const pendingPreview = view.dispatch('click', button('preview-configuration'));
    await Promise.resolve();
    const editedSetting = new FakeElement({ closest: { '#collectionTagSyncMappings': {} } });
    await view.dispatch('input', editedSetting);
    finishPreview(configurationPreview({ removals: 1 }));
    await pendingPreview;

    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.equal(view.querySelector('#collectionTagSyncConfigurationPreview').hidden, true);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /configuration changed/i);
});

test('edits made during save remain visibly unsaved after the older candidate is accepted', async () => {
    let finishSave;
    const saveResponse = new Promise(resolve => {
        finishSave = resolve;
    });
    const { calls, view } = createHarness(async path =>
        path === 'CollectionTagSync/Configuration' ? saveResponse : {});

    const pendingSave = view.dispatch('click', button('save-configuration'));
    await Promise.resolve();
    assert.equal(view.querySelector('[data-action="save-configuration"]').disabled, true);
    await view.dispatch('click', button('save-configuration'));
    const editedSetting = new FakeElement({ closest: { '#collectionTagSyncMappings': {} } });
    await view.dispatch('input', editedSetting);
    finishSave({ Outcome: 'Accepted', ActiveRevision: 2, ReconciliationId: null });
    await pendingSave;

    assert.equal(calls.filter(call => call.path === 'CollectionTagSync/Configuration').length, 1);
    assert.equal(view.querySelector('[data-action="save-configuration"]').disabled, false);
    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /earlier configuration.*saved/i);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /current edits.*unsaved/i);
});

test('edits made during confirmation remain visibly unsaved after the older candidate is accepted', async () => {
    let finishConfirmation;
    const confirmationResponse = new Promise(resolve => {
        finishConfirmation = resolve;
    });
    const { calls, view } = createHarness(async path => {
        if (path === 'CollectionTagSync/Configuration/Preview') {
            return configurationPreview({ removals: 1 });
        }

        if (path === 'CollectionTagSync/Configuration/Confirm') {
            return confirmationResponse;
        }

        return {};
    });
    await view.dispatch('click', button('preview-configuration'));

    const pendingConfirmation = view.dispatch('click', button('confirm-configuration'));
    await Promise.resolve();
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').disabled, true);
    await view.dispatch('click', button('confirm-configuration'));
    const editedSetting = new FakeElement({ closest: { '#collectionTagSyncMappings': {} } });
    await view.dispatch('input', editedSetting);
    finishConfirmation({ Outcome: 'Accepted', ActiveRevision: 2, ReconciliationId: null });
    await pendingConfirmation;

    assert.equal(calls.filter(call => call.path.endsWith('/Confirm')).length, 1);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').disabled, false);
    assert.equal(view.querySelector('[data-action="confirm-configuration"]').hidden, true);
    assert.equal(view.querySelector('[data-action="save-configuration"]').hidden, false);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /earlier configuration.*saved/i);
    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /current edits.*unsaved/i);
});

test('late save error does not overwrite the newer unsaved-editor status', async () => {
    let rejectSave;
    const saveResponse = new Promise((_resolve, reject) => {
        rejectSave = reject;
    });
    const { view } = createHarness(async path =>
        path === 'CollectionTagSync/Configuration' ? saveResponse : {});
    const pendingSave = view.dispatch('click', button('save-configuration'));
    await Promise.resolve();
    const editedSetting = new FakeElement({ closest: { '#collectionTagSyncMappings': {} } });
    await view.dispatch('input', editedSetting);

    rejectSave({ json: async () => ({ ValidationErrors: [{ Message: 'Old request failed.' }] }) });
    await pendingSave;

    assert.match(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /configuration changed/i);
    assert.doesNotMatch(view.querySelector('#collectionTagSyncConfigurationStatus').textContent, /old request failed/i);
    assert.equal(view.querySelector('[data-action="save-configuration"]').disabled, false);
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
            ItemTitle: 'Waltney Adventure',
            ItemKind: 'Movie',
            Mutations: [{
                Kind: 'AddCollectionMembership',
                Target: { Kind: 1, CollectionId: directItemId, CollectionDisplayName: 'Animation' }
            }]
        },
        {
            ItemId: cascadeItemId,
            ItemTitle: 'Waltney Adventure',
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
    assert.equal(rendered.match(/Waltney Adventure/g)?.length, 2);
    assert.ok(rendered.indexOf('Waltney Adventure') < rendered.indexOf(directItemId));
    assert.match(rendered, new RegExp(directItemId));
    assert.match(rendered, new RegExp(cascadeItemId));
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
                    Groups: [],
                    Items: [
                        { ItemId: directItemId, ItemTitle: 'Waltney Adventure' },
                        { ItemId: cascadeItemId, ItemTitle: 'Blooth Chronicle' }
                    ]
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
    assert.match(rendered, /Waltney Adventure/);
    assert.match(rendered, /Blooth Chronicle/);
    assert.ok(rendered.indexOf('Waltney Adventure') < rendered.indexOf(directItemId));
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
