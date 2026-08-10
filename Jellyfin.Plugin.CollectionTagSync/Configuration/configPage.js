const pluginId = '04920eee-c499-4b13-890f-7af0175f28f0';
const addNewCollectionValue = '__add_new_collection__';
const emptyGuid = '00000000-0000-0000-0000-000000000000';

/**
 * Treats Jellyfin's compact and hyphenated zero GUID wire forms as empty.
 *
 * @param {unknown} value possible GUID value
 * @returns {boolean} whether the value is an all-zero GUID
 */
export function isEmptyGuid(value) {
    const text = String(value ?? '').trim();
    const compact = text.replaceAll('-', '').replace(/[{}()]/g, '');
    return (compact.length === 32 && /^0{32}$/.test(compact))
        || /^\{0x0{8},0x0{4},0x0{4},\{0x0{2}(?:,0x0{2}){7}\}\}$/i.test(text);
}

/**
 * Normalizes both Jellyfin JSON enum names and editor numeric values.
 * Unknown values throw so a future server enum cannot be silently rewritten.
 *
 * @param {unknown} value node-kind wire value
 * @returns {0|1} normalized node kind
 */
export function normalizeNodeKind(value) {
    if (value === 0 || value === '0' || value === 'Tag') {
        return 0;
    }

    if (value === 1 || value === '1' || value === 'Collection') {
        return 1;
    }

    throw new TypeError(`Unsupported mapping node kind: ${String(value)}`);
}

/**
 * Normalizes both Jellyfin JSON enum names and editor numeric values.
 * Unknown values throw so a future server enum cannot be silently rewritten.
 *
 * @param {unknown} value mapping-policy wire value
 * @returns {0|1} normalized mapping policy
 */
export function normalizeMappingPolicy(value) {
    if (value === 0 || value === '0' || value === 'Additive') {
        return 0;
    }

    if (value === 1 || value === '1' || value === 'Authoritative') {
        return 1;
    }

    throw new TypeError(`Unsupported mapping policy: ${String(value)}`);
}

function property(value, name, fallback = undefined) {
    if (value && value[name] !== undefined && value[name] !== null) {
        return value[name];
    }

    const camelName = name.charAt(0).toLowerCase() + name.slice(1);
    return value && value[camelName] !== undefined && value[camelName] !== null
        ? value[camelName]
        : fallback;
}

function escapeHtml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

/**
 * Builds collection selector choices whose values are always Jellyfin GUIDs.
 * The final sentinel opens a distinct creation action and is never persisted.
 *
 * @param {Array<object>} entries current server-returned picker entries
 * @param {object|null} selectedNode an optional configured collection node
 * @returns {Array<{value: string, label: string}>} picker choices
 */
export function buildCollectionOptions(entries, selectedNode = null) {
    const configuredId = String(property(selectedNode, 'CollectionId', '') || '');
    const selectedId = isEmptyGuid(configuredId) ? '' : configuredId;
    const selectedName = String(property(selectedNode, 'CollectionDisplayName', '') || '');
    const choices = [{ value: '', label: 'Select a collection…' }];
    const known = new Set();

    for (const entry of entries ?? []) {
        const id = String(property(entry, 'Id', '') || '');
        if (!id) {
            continue;
        }

        const name = String(property(entry, 'DisplayName', '') || 'Unnamed collection');
        choices.push({ value: id, label: `${name} — ${id}` });
        known.add(id.toLowerCase());
    }

    if (selectedId && !known.has(selectedId.toLowerCase())) {
        choices.push({
            value: selectedId,
            label: `${selectedName || 'Missing collection'} — ${selectedId}`
        });
    }

    choices.push({ value: addNewCollectionValue, label: 'Add new collection…' });
    return choices;
}

/**
 * Returns only administrator-facing messages produced by server validation.
 *
 * @param {object|null} result API result
 * @returns {Array<string>} server messages
 */
export function serverValidationMessages(result) {
    return (property(result, 'ValidationErrors', []) ?? [])
        .map(error => String(property(error, 'Message', '') || ''))
        .filter(message => message.length > 0);
}

/**
 * Keeps an opaque server preview only until the corresponding editor changes.
 *
 * @returns {object} preview guard
 */
export function createPreviewGuard() {
    let remembered = null;
    return {
        remember(value) {
            remembered = value;
        },
        changed() {
            remembered = null;
        },
        value() {
            return remembered;
        },
        authorization() {
            const authorization = property(remembered, 'Authorization', null);
            return authorization ? String(authorization) : null;
        }
    };
}

/**
 * Formats all server background lifecycle states without interpreting policy.
 *
 * @param {object} status background status
 * @returns {string} administrator-facing summary
 */
export function formatBackgroundStatus(status) {
    const rawState = property(status, 'State', 0);
    const labels = new Map([
        [0, 'Queued'],
        [1, 'Running'],
        [2, 'Completed'],
        [3, 'Completed with failures'],
        [4, 'Failed'],
        [5, 'Paused for approval'],
        ['Queued', 'Queued'],
        ['Running', 'Running'],
        ['Completed', 'Completed'],
        ['PartiallyFailed', 'Completed with failures'],
        ['Failed', 'Failed'],
        ['Paused', 'Paused for approval']
    ]);
    const label = labels.get(rawState) ?? String(rawState);
    const completed = Number(property(status, 'CompletedItemCount', 0));
    const total = Number(property(status, 'TotalItemCount', 0));
    const failed = Number(property(status, 'FailedItemCount', 0));
    return `${label}: ${completed}/${total} completed, ${failed} failed.`;
}

/**
 * Converts the server mutation enum into its display direction.
 *
 * @param {number|string} kind server mutation kind
 * @returns {string} Add or Remove
 */
export function mutationDirection(kind) {
    return kind === 0
        || kind === 2
        || kind === 'AddTag'
        || kind === 'AddCollectionMembership'
        ? 'Add'
        : 'Remove';
}

function formatFullReconcileStatus(status) {
    const rawState = property(status, 'State', 0);
    const labels = new Map([
        [0, 'Idle'],
        [1, 'Planning'],
        [2, 'Applying'],
        [3, 'Awaiting approval'],
        [4, 'Completed'],
        [5, 'Completed with failures'],
        [6, 'Failed'],
        ['Idle', 'Idle'],
        ['Planning', 'Planning'],
        ['Applying', 'Applying'],
        ['AwaitingApproval', 'Awaiting approval'],
        ['Completed', 'Completed'],
        ['CompletedWithFailures', 'Completed with failures'],
        ['Failed', 'Failed']
    ]);
    const label = labels.get(rawState) ?? String(rawState);
    const succeeded = Number(property(status, 'SucceededItemCount', 0));
    const total = Number(property(status, 'TotalItemCount', 0));
    const failed = Number(property(status, 'FailedItemCount', 0));
    return `${label}: ${succeeded}/${total} succeeded, ${failed} failed.`;
}

async function requestJson(apiClient, method, path, body = undefined) {
    const options = {
        type: method,
        url: apiClient.getUrl(path),
        dataType: 'json'
    };
    if (body !== undefined) {
        options.contentType = 'application/json';
        options.data = JSON.stringify(body);
    }

    try {
        return await apiClient.ajax(options);
    } catch (error) {
        if (error && typeof error.json === 'function') {
            try {
                error.collectionTagSyncPayload = await error.json();
            } catch {
                // Keep the original HTTP error when the response has no JSON body.
            }
        }

        throw error;
    }
}

function responsePayload(error) {
    return error?.collectionTagSyncPayload ?? null;
}

function stateEquals(value, numeric, text) {
    return value === numeric || value === text;
}

export default function (view) {
    const apiClient = window.ApiClient;
    const dashboard = window.Dashboard;
    const configGuard = createPreviewGuard();
    const runOnceGuard = createPreviewGuard();
    const fullReconcileGuard = createPreviewGuard();
    const state = {
        configuration: null,
        collections: [],
        tags: [],
        collectionCreationSession: null,
        configurationEditRevision: 0,
        configurationPreviewPending: false,
        configurationMutationPending: false,
        configurationEditedDuringMutation: false,
        runOnceGroups: [],
        runOnceExcludedIds: new Set(),
        runOnceExclusionGroupId: null,
        latestFullReconcileId: null,
        queuedFullReconcileBaselineId: null,
        nextDynamicEditorId: 0,
        timers: new Set(),
        loaded: false
    };

    const query = selector => view.querySelector(selector);
    const queryAll = selector => [...view.querySelectorAll(selector)];

    function setStatus(selector, message, kind = '') {
        const container = query(selector);
        container.textContent = message ?? '';
        container.classList.remove(
            'collectionTagSyncError',
            'collectionTagSyncWarning',
            'collectionTagSyncSuccess');
        if (kind) {
            container.classList.add(`collectionTagSync${kind}`);
        }
    }

    function renderServerMessages(selector, result, fallback) {
        const messages = serverValidationMessages(result);
        setStatus(selector, messages.length > 0 ? messages.join(' ') : fallback, 'Error');
    }

    function clearTimer(timer) {
        window.clearTimeout(timer);
        state.timers.delete(timer);
    }

    function schedule(action, delay) {
        const timer = window.setTimeout(async () => {
            state.timers.delete(timer);
            await action();
        }, delay);
        state.timers.add(timer);
        return timer;
    }

    function optionHtml(option, selectedValue) {
        const selected = option.value.toLowerCase() === String(selectedValue ?? '').toLowerCase()
            ? ' selected'
            : '';
        return `<option value="${escapeHtml(option.value)}"${selected}>${escapeHtml(option.label)}</option>`;
    }

    function nodeEditorHtml(node, editorId, title, canRemove) {
        const kind = normalizeNodeKind(property(node, 'Kind', 0));
        const tagValue = String(property(node, 'TagValue', '') || '');
        const configuredCollectionId = String(property(node, 'CollectionId', '') || '');
        const collectionId = isEmptyGuid(configuredCollectionId) ? '' : configuredCollectionId;
        const collectionDisplayName = String(property(node, 'CollectionDisplayName', '') || '');
        const choices = buildCollectionOptions(state.collections, node)
            .map(option => optionHtml(option, collectionId))
            .join('');
        return `
            <div class="collectionTagSyncNode" data-node-editor
                 data-collection-display-name="${escapeHtml(collectionDisplayName)}">
                <h4>${escapeHtml(title)}</h4>
                <div class="selectContainer">
                    <label class="selectLabel" for="${editorId}Kind">Node type</label>
                    <select is="emby-select" id="${editorId}Kind" data-field="node-kind"
                            class="emby-select-withcolor emby-select">
                        <option value="0"${kind === 0 ? ' selected' : ''}>Tag</option>
                        <option value="1"${kind === 1 ? ' selected' : ''}>Collection</option>
                    </select>
                </div>
                <div class="inputContainer" data-node-kind="tag"${kind === 1 ? ' hidden' : ''}>
                    <label class="inputLabel inputLabelUnfocused" for="${editorId}Tag">Tag</label>
                    <input is="emby-input" id="${editorId}Tag" data-field="tag-value" type="text"
                           list="collectionTagSyncTagChoices" value="${escapeHtml(tagValue)}" />
                </div>
                <div class="selectContainer" data-node-kind="collection"${kind === 0 ? ' hidden' : ''}>
                    <label class="selectLabel" for="${editorId}Collection">Collection</label>
                    <select is="emby-select" id="${editorId}Collection" data-field="collection-id"
                            class="emby-select-withcolor emby-select">${choices}</select>
                </div>
                ${canRemove ? `
                    <button is="emby-button" type="button" class="raised button-warning"
                            data-action="remove-node"><span>Remove source</span></button>` : ''}
            </div>`;
    }

    function nodeSummaryLabel(node) {
        if (normalizeNodeKind(property(node, 'Kind', 0)) === 0) {
            const tagValue = String(property(node, 'TagValue', '') || '');
            return tagValue ? `Tag “${tagValue}”` : 'Tag (not selected)';
        }

        const collectionId = String(property(node, 'CollectionId', '') || '');
        const entry = state.collections.find(candidate =>
            String(property(candidate, 'Id', '')).toLowerCase() === collectionId.toLowerCase());
        if (entry) {
            return `Collection “${property(entry, 'DisplayName', 'Unnamed collection')}”`;
        }

        if (!collectionId || isEmptyGuid(collectionId)) {
            return 'Collection (not selected)';
        }

        const storedName = String(property(node, 'CollectionDisplayName', '') || '');
        return `Missing collection${storedName ? ` “${storedName}”` : ''} — ${collectionId}`;
    }

    function nodeDisplayLabel(node) {
        if (normalizeNodeKind(property(node, 'Kind', 0)) === 0) {
            return `Tag "${property(node, 'TagValue', '')}"`;
        }

        const collectionId = String(property(node, 'CollectionId', '') || '');
        const entry = state.collections.find(candidate =>
            String(property(candidate, 'Id', '')).toLowerCase() === collectionId.toLowerCase());
        const displayName = property(entry, 'DisplayName', null)
            ?? property(node, 'CollectionDisplayName', '')
            ?? 'Missing collection';
        return `Collection "${displayName}" — ${collectionId}`;
    }

    function mappingSummaryLabel(group) {
        const sources = property(group, 'Sources', []) ?? [];
        const sourceLabels = sources.length > 0
            ? sources.map(nodeSummaryLabel).join(' OR ')
            : 'No sources';
        return `${sourceLabels} → ${nodeSummaryLabel(property(group, 'Target', {}))}`;
    }

    function mappingPolicyLabel(policy) {
        return normalizeMappingPolicy(policy) === 1 ? 'Authoritative' : 'Additive';
    }

    function renderMappingGroups(expandedGroupIndex = null) {
        const container = query('#collectionTagSyncMappingGroups');
        const groups = property(state.configuration, 'MappingGroups', []) ?? [];
        if (groups.length === 0) {
            container.innerHTML = '<p class="fieldDescription">No continuous mapping groups are configured.</p>';
            return;
        }

        container.innerHTML = groups.map((group, groupIndex) => {
            const target = property(group, 'Target', {});
            const sources = property(group, 'Sources', []) ?? [];
            const policy = normalizeMappingPolicy(property(group, 'Policy', 0));
            const enabled = Boolean(property(group, 'IsEnabled', false));
            return `
                <details class="collectionTagSyncGroup" data-mapping-group
                         name="collectionTagSyncMappingEditors"${expandedGroupIndex === groupIndex ? ' open' : ''}>
                    <summary class="collectionTagSyncGroupSummary" data-action="edit-mapping">
                        <span class="collectionTagSyncMappingFlow" data-role="mapping-summary-flow">${escapeHtml(mappingSummaryLabel(group))}</span>
                        <span class="collectionTagSyncMappingMeta">
                            <span data-role="mapping-summary-policy">${mappingPolicyLabel(policy)}</span>
                            <span aria-hidden="true"> · </span>
                            <span data-role="mapping-summary-state">${enabled ? 'Enabled' : 'Disabled'}</span>
                            <span class="collectionTagSyncEditLabel">Edit</span>
                        </span>
                    </summary>
                    <div data-role="mapping-editor">
                        <h3>Edit mapping group ${groupIndex + 1}</h3>
                        <div data-role="target">
                            ${nodeEditorHtml(target, `mapping${groupIndex}Target`, 'Target', false)}
                        </div>
                        <h4>Sources</h4>
                        <div data-role="sources">
                            ${sources.map((source, sourceIndex) => nodeEditorHtml(
                                source,
                                `mapping${groupIndex}Source${sourceIndex}`,
                                `Source ${sourceIndex + 1}`,
                                true)).join('')}
                        </div>
                        <button is="emby-button" type="button" class="raised button"
                                data-action="add-mapping-source"><span>Add source</span></button>
                        <div class="selectContainer">
                            <label class="selectLabel" for="mapping${groupIndex}Policy">Policy</label>
                            <select is="emby-select" id="mapping${groupIndex}Policy" data-field="policy"
                                    class="emby-select-withcolor emby-select">
                                <option value="0"${policy === 0 ? ' selected' : ''}>Additive — preserve manual target state</option>
                                <option value="1"${policy === 1 ? ' selected' : ''}>Authoritative — remove unsupported target state</option>
                            </select>
                        </div>
                        <div class="checkboxContainer checkboxContainer-withDescription">
                            <label class="emby-checkbox-label">
                                <input is="emby-checkbox" type="checkbox" data-field="enabled"${enabled ? ' checked' : ''} />
                                <span>Enabled</span>
                            </label>
                        </div>
                        <button is="emby-button" type="button" class="raised button-warning"
                                data-action="remove-mapping"><span>Delete mapping group</span></button>
                    </div>
                </details>`;
        }).join('');
    }

    function renderSettings() {
        query('#collectionTagSyncStartupDelay').value = property(
            state.configuration, 'StartupReconcileDelayMinutes', 5);
        query('#collectionTagSyncCircuitBreakerEnabled').checked = Boolean(property(
            state.configuration, 'DestructiveCircuitBreakerEnabled', true));
        query('#collectionTagSyncMaximumItems').value = property(
            state.configuration, 'DestructiveMaximumAffectedItems', 25);
        query('#collectionTagSyncMaximumPercentage').value = property(
            state.configuration, 'DestructiveMaximumRemovalPercentage', 20);
        query('#collectionTagSyncMinimumPopulation').value = property(
            state.configuration, 'DestructiveMinimumAssignmentPopulation', 10);
        query('#collectionTagSyncCircuitBreakerAcknowledged').checked = Boolean(property(
            state.configuration, 'DestructiveCircuitBreakerDisableAcknowledged', false));
        updateCircuitBreakerWarning();
    }

    function renderTagChoices() {
        query('#collectionTagSyncTagChoices').innerHTML = state.tags
            .map(tag => `<option value="${escapeHtml(tag)}"></option>`)
            .join('');
    }

    function renderRunOnceGroups(expandedGroupIndex = null) {
        const container = query('#collectionTagSyncRunOnceGroups');
        if (state.runOnceGroups.length === 0) {
            container.innerHTML = '<p class="fieldDescription">No reusable run-once groups are saved.</p>';
            return;
        }

        container.innerHTML = state.runOnceGroups.map((group, groupIndex) => {
            const target = property(group, 'Target', {});
            const sources = property(group, 'Sources', []) ?? [];
            const policy = normalizeMappingPolicy(property(group, 'Policy', 0));
            const groupId = String(property(group, 'Id', '') || '');
            const isSaved = groupId && !isEmptyGuid(groupId);
            const editorIsDirty = Boolean(property(group, 'EditorIsDirty', false));
            return `
                <details class="collectionTagSyncGroup" data-run-once-group
                         data-group-id="${escapeHtml(isSaved ? groupId : '')}"
                         data-editor-dirty="${editorIsDirty}"
                         name="collectionTagSyncRunOnceEditors"${expandedGroupIndex === groupIndex ? ' open' : ''}>
                    <summary class="collectionTagSyncGroupSummary" data-action="edit-run-once-group">
                        <span class="collectionTagSyncMappingFlow" data-role="run-once-summary-flow">${escapeHtml(mappingSummaryLabel(group))}</span>
                        <span class="collectionTagSyncMappingMeta">
                            <span data-role="run-once-summary-policy">${mappingPolicyLabel(policy)}</span>
                            <span aria-hidden="true"> · </span>
                            <span data-role="run-once-summary-state">${isSaved && !editorIsDirty ? 'Saved' : 'Unsaved'}</span>
                            <span class="collectionTagSyncEditLabel">Edit</span>
                        </span>
                    </summary>
                    <div data-role="run-once-editor">
                        <h3>${isSaved ? `Edit run-once group ${groupIndex + 1}` : 'Add run-once group'}</h3>
                        <div data-role="run-once-target">
                            ${nodeEditorHtml(target, `runOnce${groupIndex}Target`, 'Target', false)}
                        </div>
                        <h4>Sources</h4>
                        <div data-role="run-once-sources">
                            ${sources.map((source, sourceIndex) => nodeEditorHtml(
                                source,
                                `runOnce${groupIndex}Source${sourceIndex}`,
                                `Source ${sourceIndex + 1}`,
                                true)).join('')}
                        </div>
                        <button is="emby-button" type="button" class="raised button"
                                data-action="add-run-once-source"><span>Add source</span></button>
                        <div class="selectContainer">
                            <label class="selectLabel" for="runOnce${groupIndex}Policy">Policy</label>
                            <select is="emby-select" id="runOnce${groupIndex}Policy" data-field="policy"
                                    class="emby-select-withcolor emby-select">
                                <option value="0"${policy === 0 ? ' selected' : ''}>Additive — preserve manual target state</option>
                                <option value="1"${policy === 1 ? ' selected' : ''}>Authoritative — remove unsupported target state</option>
                            </select>
                        </div>
                        <div class="collectionTagSyncActions">
                            <button is="emby-button" type="button" class="raised button-submit"
                                    data-action="save-run-once-group"><span>Save group</span></button>
                            <button is="emby-button" type="button" class="raised button"
                                    data-action="preview-run-once"${isSaved && !editorIsDirty ? '' : ' disabled'}><span>Preview group</span></button>
                            <button is="emby-button" type="button" class="raised button-warning"
                                    data-action="delete-run-once-group"><span>Delete group</span></button>
                        </div>
                    </div>
                </details>`;
        }).join('');
    }

    function updateNodeVisibility(editor) {
        const kind = Number(editor.querySelector('[data-field="node-kind"]').value);
        editor.querySelector('[data-node-kind="tag"]').hidden = kind !== 0;
        editor.querySelector('[data-node-kind="collection"]').hidden = kind !== 1;
    }

    function fillCollectionSelect(select, selectedValue = '') {
        const editor = select.closest('[data-node-editor]');
        const selectedNode = {
            CollectionId: selectedValue,
            CollectionDisplayName: editor?.dataset.collectionDisplayName ?? ''
        };
        select.innerHTML = buildCollectionOptions(state.collections, selectedNode)
            .map(option => optionHtml(option, selectedValue))
            .join('');
    }

    function refreshCollectionSelects() {
        for (const select of queryAll('[data-field="collection-id"]')) {
            fillCollectionSelect(select, select.value);
        }
    }

    function readNode(editor) {
        const kind = Number(editor.querySelector('[data-field="node-kind"]').value);
        if (kind === 0) {
            return {
                Kind: 0,
                TagValue: editor.querySelector('[data-field="tag-value"]').value,
                CollectionId: emptyGuid,
                CollectionDisplayName: ''
            };
        }

        const collectionId = editor.querySelector('[data-field="collection-id"]').value;
        const entry = state.collections.find(candidate =>
            String(property(candidate, 'Id', '')).toLowerCase() === collectionId.toLowerCase());
        return {
            Kind: 1,
            TagValue: '',
            CollectionId: collectionId || emptyGuid,
            CollectionDisplayName: entry
                ? String(property(entry, 'DisplayName', '') || '')
                : editor.dataset.collectionDisplayName ?? ''
        };
    }

    function readMappingGroup(group) {
        return {
            Target: readNode(group.querySelector('[data-role="target"] [data-node-editor]')),
            Sources: [...group.querySelectorAll('[data-role="sources"] [data-node-editor]')]
                .map(readNode),
            Policy: Number(group.querySelector('[data-field="policy"]').value),
            IsEnabled: group.querySelector('[data-field="enabled"]').checked
        };
    }

    function readCandidate() {
        return {
            SchemaVersion: Number(property(state.configuration, 'SchemaVersion', 1)),
            Revision: Number(property(state.configuration, 'Revision', 0)),
            StartupReconcileDelayMinutes: Number(query('#collectionTagSyncStartupDelay').value),
            DestructiveCircuitBreakerEnabled: query('#collectionTagSyncCircuitBreakerEnabled').checked,
            DestructiveMaximumAffectedItems: Number(query('#collectionTagSyncMaximumItems').value),
            DestructiveMaximumRemovalPercentage: Number(query('#collectionTagSyncMaximumPercentage').value),
            DestructiveMinimumAssignmentPopulation: Number(query('#collectionTagSyncMinimumPopulation').value),
            DestructiveCircuitBreakerDisableAcknowledged:
                query('#collectionTagSyncCircuitBreakerAcknowledged').checked,
            PausedFullReconcile: property(state.configuration, 'PausedFullReconcile', null),
            MappingGroups: queryAll('[data-mapping-group]').map(readMappingGroup)
        };
    }

    function readRunOnceGroup(group) {
        return {
            Id: group.dataset.groupId || emptyGuid,
            Target: readNode(group.querySelector('[data-role="run-once-target"] [data-node-editor]')),
            Sources: [...group.querySelectorAll('[data-role="run-once-sources"] [data-node-editor]')]
                .map(readNode),
            Policy: Number(group.querySelector('[data-field="policy"]').value)
        };
    }

    function readRunOnceEditorGroup(group) {
        return {
            ...readRunOnceGroup(group),
            EditorIsDirty: group.dataset.editorDirty === 'true'
        };
    }

    function readRunOnceOperation(group) {
        return {
            GroupId: group.dataset.groupId,
            ExcludedItemIds: [...state.runOnceExcludedIds]
        };
    }

    function addMappingGroup() {
        const candidate = readCandidate();
        candidate.MappingGroups.push({
            Target: { Kind: 0, TagValue: '', CollectionId: emptyGuid, CollectionDisplayName: '' },
            Sources: [{ Kind: 0, TagValue: '', CollectionId: emptyGuid, CollectionDisplayName: '' }],
            Policy: 0,
            IsEnabled: true
        });
        state.configuration = { ...state.configuration, MappingGroups: candidate.MappingGroups };
        renderMappingGroups(candidate.MappingGroups.length - 1);
        configurationChanged();
    }

    function addRunOnceGroup() {
        const groups = queryAll('[data-run-once-group]').map(readRunOnceEditorGroup);
        groups.push({
            Id: emptyGuid,
            Target: { Kind: 0, TagValue: '', CollectionId: emptyGuid, CollectionDisplayName: '' },
            Sources: [{ Kind: 0, TagValue: '', CollectionId: emptyGuid, CollectionDisplayName: '' }],
            Policy: 0
        });
        state.runOnceGroups = groups;
        renderRunOnceGroups(groups.length - 1);
        runOnceChanged(null);
    }

    function addSource(container, prefix) {
        const index = container.querySelectorAll('[data-node-editor]').length;
        container.insertAdjacentHTML('beforeend', nodeEditorHtml(
            { Kind: 0, TagValue: '' },
            `${prefix}${state.nextDynamicEditorId++}Source${index}`,
            `Source ${index + 1}`,
            true));
    }

    function configurationChanged() {
        const preview = query('#collectionTagSyncConfigurationPreview');
        const confirmation = query('[data-action="confirm-configuration"]');
        const invalidatedMutation = state.configurationMutationPending
            && !state.configurationEditedDuringMutation;
        const invalidatedWorkflow = !preview.hidden
            || !confirmation.hidden
            || state.configurationPreviewPending
            || invalidatedMutation;
        state.configurationEditRevision++;
        state.configurationPreviewPending = false;
        if (state.configurationMutationPending) {
            state.configurationEditedDuringMutation = true;
        }

        configGuard.changed();
        query('[data-action="save-configuration"]').hidden = false;
        confirmation.hidden = true;
        preview.hidden = true;
        if (invalidatedWorkflow) {
            setStatus(
                '#collectionTagSyncConfigurationStatus',
                'Configuration changed. Save configuration or preview changes when ready.',
                'Warning');
        }
    }

    function showDestructiveConfigurationConfirmation() {
        query('[data-action="save-configuration"]').hidden = true;
        query('[data-action="confirm-configuration"]').hidden = false;
    }

    function setConfigurationMutationPending(pending) {
        state.configurationMutationPending = pending;
        for (const selector of [
            '[data-action="save-configuration"]',
            '[data-action="confirm-configuration"]',
            '[data-action="preview-configuration"]'
        ]) {
            query(selector).disabled = pending;
        }
    }

    function beginConfigurationMutation() {
        if (state.configurationMutationPending) {
            return null;
        }

        state.configurationEditedDuringMutation = false;
        setConfigurationMutationPending(true);
        return state.configurationEditRevision;
    }

    function finishConfigurationMutation(submittedEditRevision) {
        const editorChanged = state.configurationEditedDuringMutation
            || state.configurationEditRevision !== submittedEditRevision;
        setConfigurationMutationPending(false);
        state.configurationEditedDuringMutation = false;
        return editorChanged;
    }

    function pollConfigurationReconciliation(result) {
        const reconciliationId = property(result, 'ReconciliationId', null);
        if (reconciliationId) {
            pollBackground('Configuration', reconciliationId, '#collectionTagSyncConfigurationReconciliation');
        }

        return reconciliationId;
    }

    function configurationSaved(result) {
        configurationChanged();
        const reconciliationId = pollConfigurationReconciliation(result);
        setStatus(
            '#collectionTagSyncConfigurationStatus',
            reconciliationId
                ? 'Configuration saved. Metadata changes were queued and will settle in the background.'
                : 'Configuration saved. No metadata changes were queued.',
            'Success');
    }

    function earlierConfigurationSaved(result) {
        pollConfigurationReconciliation(result);
        setStatus(
            '#collectionTagSyncConfigurationStatus',
            'The earlier configuration was saved, but current edits remain unsaved. Save configuration again when ready.',
            'Warning');
    }

    function updateMappingSummary(changedElement) {
        const group = changedElement?.closest('[data-mapping-group]');
        if (!group) {
            return;
        }

        const mapping = readMappingGroup(group);
        group.querySelector('[data-role="mapping-summary-flow"]').textContent = mappingSummaryLabel(mapping);
        group.querySelector('[data-role="mapping-summary-policy"]').textContent = mappingPolicyLabel(mapping.Policy);
        group.querySelector('[data-role="mapping-summary-state"]').textContent = mapping.IsEnabled
            ? 'Enabled'
            : 'Disabled';
    }

    function updateRunOnceSummary(changedElement) {
        const group = changedElement?.closest('[data-run-once-group]');
        if (!group) {
            return;
        }

        const operation = readRunOnceGroup(group);
        group.querySelector('[data-role="run-once-summary-flow"]').textContent = mappingSummaryLabel(operation);
        group.querySelector('[data-role="run-once-summary-policy"]').textContent = mappingPolicyLabel(operation.Policy);
        group.querySelector('[data-role="run-once-summary-state"]').textContent =
            group.dataset.editorDirty === 'true' || !group.dataset.groupId ? 'Unsaved' : 'Saved';
    }

    function clearRunOnceExclusions() {
        state.runOnceExcludedIds.clear();
        state.runOnceExclusionGroupId = null;
    }

    function runOnceChanged(changedElement = null, clearExclusions = true) {
        if (clearExclusions) {
            clearRunOnceExclusions();
        }

        const group = changedElement?.closest?.('[data-run-once-group]');
        if (group) {
            group.dataset.editorDirty = 'true';
            updateRunOnceSummary(group);
        }

        runOnceGuard.changed();
        query('[data-action="confirm-run-once"]').hidden = true;
        setStatus(
            '#collectionTagSyncRunOnceStatus',
            group
                ? 'The group changed. Save it before previewing.'
                : 'The operation changed. Preview again before running.',
            'Warning');
    }

    function updateCircuitBreakerWarning() {
        const enabled = query('#collectionTagSyncCircuitBreakerEnabled').checked;
        query('#collectionTagSyncCircuitBreakerWarning').hidden = enabled;
        if (enabled) {
            query('#collectionTagSyncCircuitBreakerAcknowledged').checked = false;
        }
    }

    function planSummary(preview) {
        const items = property(preview, 'Items', []) ?? [];
        let additions = 0;
        let removals = 0;
        for (const item of items) {
            for (const mutation of property(item, 'Mutations', []) ?? []) {
                const kind = property(mutation, 'Kind', 0);
                if (mutationDirection(kind) === 'Add') {
                    additions += 1;
                } else {
                    removals += 1;
                }
            }
        }

        return {
            items,
            additions,
            removals,
            total: Number(property(preview, 'TotalItemCount', items.length))
        };
    }

    function mutationLabel(mutation) {
        const kind = property(mutation, 'Kind', 0);
        const target = property(mutation, 'Target', {});
        const targetKind = Number(property(target, 'Kind', 0));
        const targetLabel = targetKind === 0
            ? `Tag "${property(target, 'TagValue', '')}"`
            : `Collection "${property(target, 'CollectionDisplayName', '')}" (${property(target, 'CollectionId', '')})`;
        return `${mutationDirection(kind)} ${targetLabel}`;
    }

    function itemKindLabel(kind) {
        if (kind === 0 || kind === 'Movie') {
            return 'Movie';
        }

        if (kind === 1 || kind === 'Series') {
            return 'Series';
        }

        return String(kind ?? 'Item');
    }

    function renderPlanPreview(selector, authorization, excludableItemIds = null) {
        const preview = property(authorization, 'Preview', {});
        const excludable = excludableItemIds === null
            ? null
            : new Set(excludableItemIds.map(itemId => String(itemId)));
        const summary = planSummary(preview);
        const changedItems = summary.items.filter(item =>
            (property(item, 'Mutations', []) ?? []).length > 0);
        const displayedItems = summary.items.filter(item => {
            const itemId = String(property(item, 'ItemId', '') || '');
            return (property(item, 'Mutations', []) ?? []).length > 0
                || (excludable !== null && state.runOnceExcludedIds.has(itemId));
        });
        const itemMarkup = displayedItems.map(item => {
            const itemId = String(property(item, 'ItemId', '') || '');
            const itemKind = itemKindLabel(property(item, 'ItemKind', 'Item'));
            const itemTitle = String(property(item, 'ItemTitle', '') || '').trim();
            const primaryLabel = itemTitle || `Untitled ${itemKind}`;
            const mutations = (property(item, 'Mutations', []) ?? [])
                .map(mutation => `<li class="collectionTagSyncMutation">${escapeHtml(mutationLabel(mutation))}</li>`)
                .join('');
            const exclusion = excludable?.has(itemId)
                ? `<label class="emby-checkbox-label">
                       <input is="emby-checkbox" type="checkbox" data-run-once-exclusion="${escapeHtml(itemId)}"
                              ${state.runOnceExcludedIds.has(itemId) ? 'checked' : ''} />
                       <span>Retain this item's observed target state</span>
                   </label>`
                : '';
            return `<li><strong>${escapeHtml(primaryLabel)}</strong>
                        <div class="fieldDescription">${escapeHtml(itemKind)} · Item ID: ${escapeHtml(itemId)}</div>
                        ${exclusion}<ul>${mutations}</ul></li>`;
        }).join('');
        const expires = property(authorization, 'ExpiresAtUtc', '');
        const container = query(selector);
        container.innerHTML = `
            <p><strong>${summary.additions}</strong> additions and <strong>${summary.removals}</strong> removals
               across ${changedItems.length} changed items (${summary.total} eligible).</p>
            <p class="fieldDescription">Authorization expires ${escapeHtml(expires)}.</p>
            ${displayedItems.length > 0 ? `<ul>${itemMarkup}</ul>` : '<p>No direct mutations are planned.</p>'}`;
        container.hidden = false;
    }

    async function activateConfiguration() {
        if (state.configurationMutationPending) {
            return;
        }

        const candidate = readCandidate();
        configurationChanged();
        const submittedEditRevision = beginConfigurationMutation();
        setStatus('#collectionTagSyncConfigurationStatus', 'Validating and saving with the server…');
        try {
            const result = await requestJson(apiClient, 'POST', 'CollectionTagSync/Configuration', candidate);
            const editorChanged = finishConfigurationMutation(submittedEditRevision);
            state.configuration = {
                ...candidate,
                Revision: property(result, 'ActiveRevision', candidate.Revision),
                PausedFullReconcile: null
            };
            if (editorChanged) {
                earlierConfigurationSaved(result);
            } else {
                configurationSaved(result);
            }
        } catch (error) {
            const editorChanged = finishConfigurationMutation(submittedEditRevision);
            if (editorChanged) {
                return;
            }

            const result = responsePayload(error);
            const outcome = property(result, 'Outcome', null);
            if (stateEquals(outcome, 2, 'RequiresPreview')) {
                await previewConfiguration();
                return;
            }

            configurationChanged();
            renderServerMessages(
                '#collectionTagSyncConfigurationStatus',
                result,
                'The server rejected the configuration.');
        }
    }

    async function previewConfiguration() {
        if (state.configurationMutationPending) {
            return;
        }

        const candidate = readCandidate();
        configurationChanged();
        const previewRevision = state.configurationEditRevision;
        state.configurationPreviewPending = true;
        setStatus('#collectionTagSyncConfigurationStatus', 'Calculating the server preview…');
        try {
            const result = await requestJson(apiClient, 'POST', 'CollectionTagSync/Configuration/Preview', candidate);
            if (!state.configurationPreviewPending
                || state.configurationEditRevision !== previewRevision) {
                return;
            }

            state.configurationPreviewPending = false;
            const authorization = property(result, 'Authorization', null);
            configGuard.remember({ Authorization: property(authorization, 'Authorization', ''), Candidate: candidate });
            renderPlanPreview('#collectionTagSyncConfigurationPreview', authorization);
            const summary = planSummary(property(authorization, 'Preview', {}));
            if (summary.removals > 0) {
                showDestructiveConfigurationConfirmation();
                setStatus(
                    '#collectionTagSyncConfigurationStatus',
                    'Preview ready. No changes have been saved. Review the removals, then confirm removals and save.',
                    'Warning');
                query('#collectionTagSyncConfigurationPreview').focus();
            } else {
                query('[data-action="save-configuration"]').hidden = false;
                query('[data-action="confirm-configuration"]').hidden = true;
                setStatus(
                    '#collectionTagSyncConfigurationStatus',
                    'Preview ready. No changes have been saved. Choose Save configuration to accept it.',
                    'Success');
            }
        } catch (error) {
            if (!state.configurationPreviewPending
                || state.configurationEditRevision !== previewRevision) {
                return;
            }

            state.configurationPreviewPending = false;
            configurationChanged();
            renderServerMessages(
                '#collectionTagSyncConfigurationStatus',
                responsePayload(error),
                'The server could not preview this configuration.');
        }
    }

    async function confirmConfiguration() {
        if (state.configurationMutationPending) {
            return;
        }

        const remembered = configGuard.value();
        const authorization = configGuard.authorization();
        if (!remembered || !authorization) {
            query('[data-action="save-configuration"]').hidden = false;
            query('[data-action="confirm-configuration"]').hidden = true;
            setStatus('#collectionTagSyncConfigurationStatus', 'The preview is stale. Preview again.', 'Warning');
            return;
        }

        const submittedEditRevision = beginConfigurationMutation();
        setStatus('#collectionTagSyncConfigurationStatus', 'Recomputing approved removals and saving…');
        try {
            const result = await requestJson(apiClient, 'POST', 'CollectionTagSync/Configuration/Confirm', {
                Candidate: remembered.Candidate,
                Authorization: authorization
            });
            const editorChanged = finishConfigurationMutation(submittedEditRevision);
            state.configuration = {
                ...remembered.Candidate,
                Revision: property(result, 'ActiveRevision', remembered.Candidate.Revision),
                PausedFullReconcile: null
            };
            if (editorChanged) {
                earlierConfigurationSaved(result);
            } else {
                configurationSaved(result);
            }
        } catch (error) {
            const editorChanged = finishConfigurationMutation(submittedEditRevision);
            if (editorChanged) {
                return;
            }

            configurationChanged();
            const payload = responsePayload(error);
            const messages = serverValidationMessages(payload);
            setStatus(
                '#collectionTagSyncConfigurationStatus',
                messages.length > 0
                    ? messages.join(' ')
                    : 'The preview expired or the candidate, active revision, or removal set changed. Preview again.',
                'Warning');
        }
    }

    async function saveRunOnceGroup(group) {
        const candidate = readRunOnceGroup(group);
        setStatus('#collectionTagSyncRunOnceStatus', 'Validating and saving the group…');
        try {
            const result = await requestJson(
                apiClient,
                'POST',
                'CollectionTagSync/RunOnce/Groups',
                candidate);
            const saved = property(result, 'Group', null);
            if (!saved) {
                throw new Error('The server did not return the saved group.');
            }

            const groups = queryAll('[data-run-once-group]').map(readRunOnceEditorGroup);
            const index = queryAll('[data-run-once-group]').indexOf(group);
            groups[index] = { ...saved, EditorIsDirty: false };
            state.runOnceGroups = groups;
            runOnceGuard.changed();
            clearRunOnceExclusions();
            query('[data-action="confirm-run-once"]').hidden = true;
            renderRunOnceGroups(index);
            setStatus('#collectionTagSyncRunOnceStatus', 'Run-once group saved.', 'Success');
        } catch (error) {
            renderServerMessages(
                '#collectionTagSyncRunOnceStatus',
                responsePayload(error),
                'The server could not save this run-once group.');
        }
    }

    async function deleteRunOnceGroup(group) {
        const groupId = group.dataset.groupId;
        try {
            if (groupId) {
                await requestJson(
                    apiClient,
                    'DELETE',
                    `CollectionTagSync/RunOnce/Groups/${encodeURIComponent(groupId)}`);
            }

            state.runOnceGroups = queryAll('[data-run-once-group]')
                .filter(candidate => candidate !== group)
                .map(readRunOnceEditorGroup);
            runOnceGuard.changed();
            clearRunOnceExclusions();
            query('[data-action="confirm-run-once"]').hidden = true;
            renderRunOnceGroups();
            setStatus('#collectionTagSyncRunOnceStatus', 'Run-once group deleted.', 'Success');
        } catch (error) {
            renderServerMessages(
                '#collectionTagSyncRunOnceStatus',
                responsePayload(error),
                'The server could not delete this run-once group.');
        }
    }

    async function previewRunOnce(group) {
        if (!group?.dataset.groupId || group.dataset.editorDirty === 'true') {
            setStatus('#collectionTagSyncRunOnceStatus', 'Save the group before previewing it.', 'Warning');
            return;
        }

        const groupId = group.dataset.groupId;
        if (state.runOnceExclusionGroupId && state.runOnceExclusionGroupId !== groupId) {
            clearRunOnceExclusions();
        }

        const operation = readRunOnceOperation(group);
        runOnceGuard.changed();
        query('[data-action="confirm-run-once"]').hidden = true;
        setStatus('#collectionTagSyncRunOnceStatus', 'Calculating the server preview…');
        try {
            const result = await requestJson(apiClient, 'POST', 'CollectionTagSync/RunOnce/Preview', operation);
            const authorization = property(result, 'Authorization', null);
            runOnceGuard.remember({ Authorization: property(authorization, 'Authorization', ''), Operation: operation });
            state.runOnceExclusionGroupId = groupId;
            renderPlanPreview(
                '#collectionTagSyncRunOncePreview',
                authorization,
                property(authorization, 'ExcludableItemIds', []) ?? []);
            query('[data-action="confirm-run-once"]').hidden = false;
            setStatus('#collectionTagSyncRunOnceStatus', 'Preview ready. Review it before running.', 'Success');
        } catch (error) {
            runOnceGuard.changed();
            clearRunOnceExclusions();
            query('[data-action="confirm-run-once"]').hidden = true;
            renderServerMessages(
                '#collectionTagSyncRunOnceStatus',
                responsePayload(error),
                'The server could not preview this run-once operation.');
        }
    }

    async function confirmRunOnce() {
        const remembered = runOnceGuard.value();
        const authorization = runOnceGuard.authorization();
        if (!remembered || !authorization) {
            setStatus('#collectionTagSyncRunOnceStatus', 'The preview is stale. Preview again.', 'Warning');
            return;
        }

        setStatus('#collectionTagSyncRunOnceStatus', 'Recomputing and queuing with the server…');
        try {
            const result = await requestJson(apiClient, 'POST', 'CollectionTagSync/RunOnce/Confirm', {
                Operation: remembered.Operation,
                Authorization: authorization
            });
            runOnceGuard.changed();
            query('[data-action="confirm-run-once"]').hidden = true;
            setStatus('#collectionTagSyncRunOnceStatus', 'Run-once execution queued.', 'Success');
            const reconciliationId = property(result, 'ReconciliationId', null);
            if (reconciliationId) {
                pollBackground('RunOnce', reconciliationId, '#collectionTagSyncRunOnceReconciliation');
            }
        } catch (error) {
            runOnceGuard.changed();
            query('[data-action="confirm-run-once"]').hidden = true;
            const payload = responsePayload(error);
            const messages = serverValidationMessages(payload);
            setStatus(
                '#collectionTagSyncRunOnceStatus',
                messages.length > 0
                    ? messages.join(' ')
                    : 'The preview expired or the operation, exclusions, active revision, or removal set changed. Preview again.',
                'Warning');
        } finally {
            clearRunOnceExclusions();
        }
    }

    async function pollBackground(kind, id, selector) {
        try {
            const status = await requestJson(
                apiClient,
                'GET',
                `CollectionTagSync/${kind}/Reconciliations/${encodeURIComponent(id)}`);
            setStatus(selector, formatBackgroundStatus(status));
            const lifecycle = property(status, 'State', 0);
            if (stateEquals(lifecycle, 0, 'Queued') || stateEquals(lifecycle, 1, 'Running')) {
                schedule(() => pollBackground(kind, id, selector), 1000);
            }
        } catch {
            setStatus(selector, 'Background status is temporarily unavailable.', 'Warning');
        }
    }

    async function queueFullReconcile() {
        setStatus('#collectionTagSyncFullReconcileStatus', 'Queueing Full Reconcile…');
        try {
            state.queuedFullReconcileBaselineId = state.latestFullReconcileId;
            await requestJson(apiClient, 'POST', 'CollectionTagSync/FullReconcile');
            setStatus('#collectionTagSyncFullReconcileStatus', 'Full Reconcile queued.', 'Success');
            schedule(refreshFullReconcileStatus, 250);
        } catch {
            setStatus('#collectionTagSyncFullReconcileStatus', 'The server could not queue Full Reconcile.', 'Error');
        }
    }

    function renderPausedFullReconcile(authorization) {
        const preview = property(authorization, 'Preview', {});
        const removals = property(preview, 'Removals', []) ?? [];
        const groups = property(preview, 'Groups', []) ?? [];
        const itemsById = new Map((property(preview, 'Items', []) ?? []).map(item => [
            String(property(item, 'ItemId', '') || ''),
            item
        ]));
        const container = query('#collectionTagSyncFullReconcilePreview');
        container.innerHTML = `
            <p><strong>${escapeHtml(property(preview, 'UniqueAffectedItemCount', 0))}</strong> unique items are affected,
               with <strong>${removals.length}</strong> Authoritative removals.</p>
            <p>${property(preview, 'ExceedsAbsoluteLimit', false)
                ? 'The absolute affected-item limit was exceeded.'
                : 'One or more per-group limits were exceeded.'}</p>
            <h4>Item-level removals</h4>
            <ul>${removals.map(removal => {
                const itemId = String(property(removal, 'ItemId', '') || '');
                const item = itemsById.get(itemId) ?? {};
                const itemKind = itemKindLabel(property(item, 'ItemKind', 'Item'));
                const itemTitle = String(property(item, 'ItemTitle', '') || '').trim();
                return `<li><strong>${escapeHtml(itemTitle || `Untitled ${itemKind}`)}</strong>
                    <div class="fieldDescription">${escapeHtml(itemKind)} · Item ID: ${escapeHtml(itemId)}</div>
                    ${escapeHtml(mutationDirection(property(removal, 'Kind', '')))}
                    ${escapeHtml(nodeDisplayLabel(property(removal, 'Target', {})))}</li>`;
            }).join('')}</ul>
            <h4>Removal limits by target</h4>
            <ul>${groups.map(group => `
                <li>${escapeHtml(nodeDisplayLabel(property(group, 'Target', {})))}:
                    ${escapeHtml(property(group, 'RemovalCount', 0))}
                    removals from ${escapeHtml(property(group, 'CurrentAssignmentCount', 0))} current assignments.</li>`).join('')}</ul>`;
        container.hidden = false;
    }

    async function refreshFullReconcileStatus() {
        try {
            const status = await requestJson(apiClient, 'GET', 'CollectionTagSync/FullReconcile/Status');
            setStatus('#collectionTagSyncFullReconcileStatus', formatFullReconcileStatus(status));
            const lifecycle = property(status, 'State', 0);
            const runId = property(status, 'Id', null);
            state.latestFullReconcileId = runId;
            if (state.queuedFullReconcileBaselineId
                && runId === state.queuedFullReconcileBaselineId) {
                schedule(refreshFullReconcileStatus, 500);
                return;
            }

            state.queuedFullReconcileBaselineId = null;
            if (stateEquals(lifecycle, 3, 'AwaitingApproval') && runId) {
                if (!fullReconcileGuard.authorization()) {
                    const authorization = await requestJson(
                        apiClient,
                        'POST',
                        `CollectionTagSync/FullReconcile/${encodeURIComponent(runId)}/Preview`);
                    fullReconcileGuard.remember({
                        Authorization: property(authorization, 'Authorization', ''),
                        RunId: runId
                    });
                    renderPausedFullReconcile(authorization);
                }

                query('[data-action="confirm-full-reconcile"]').hidden = false;
                return;
            }

            fullReconcileGuard.changed();
            query('[data-action="confirm-full-reconcile"]').hidden = true;
            query('#collectionTagSyncFullReconcilePreview').hidden = true;
            if (stateEquals(lifecycle, 1, 'Planning') || stateEquals(lifecycle, 2, 'Applying')) {
                schedule(refreshFullReconcileStatus, 1000);
            }
        } catch {
            setStatus('#collectionTagSyncFullReconcileStatus', 'Full Reconcile status is unavailable.', 'Warning');
        }
    }

    async function confirmFullReconcile() {
        const remembered = fullReconcileGuard.value();
        const authorization = fullReconcileGuard.authorization();
        if (!remembered || !authorization) {
            setStatus('#collectionTagSyncFullReconcileStatus', 'The approval is stale. Refresh status.', 'Warning');
            return;
        }

        try {
            const result = await requestJson(
                apiClient,
                'POST',
                `CollectionTagSync/FullReconcile/${encodeURIComponent(remembered.RunId)}/Confirm`,
                { Authorization: authorization });
            fullReconcileGuard.changed();
            query('[data-action="confirm-full-reconcile"]').hidden = true;
            setStatus(
                '#collectionTagSyncFullReconcileStatus',
                formatFullReconcileStatus(property(result, 'RunResult', {})),
                'Success');
            await refreshOperationalStatus();
        } catch {
            fullReconcileGuard.changed();
            query('[data-action="confirm-full-reconcile"]').hidden = true;
            setStatus(
                '#collectionTagSyncFullReconcileStatus',
                'The approval expired or the active configuration or removal set changed. Refresh and review again.',
                'Warning');
            await refreshFullReconcileStatus();
        }
    }

    async function refreshOperationalStatus() {
        try {
            const status = await requestJson(apiClient, 'GET', 'CollectionTagSync/Status');
            const incremental = property(status, 'Incremental', {});
            const requested = property(status, 'FullReconcileRequest', {});
            const reasons = property(requested, 'Reasons', []) ?? [];
            setStatus(
                '#collectionTagSyncOperationalStatus',
                `Incremental: ${property(incremental, 'QueuedItemCount', 0)} queued, `
                + `${property(incremental, 'RunningItemCount', 0)} running, `
                + `${property(incremental, 'QuarantinedItemCount', 0)} deferred failures. `
                + `${property(incremental, 'IsStormFallbackActive', false)
                    ? 'Event-storm fallback is active. '
                    : ''}`
                + `${property(requested, 'IsRequested', false)
                    ? `Full Reconcile requested (${reasons.join(', ')}).`
                    : 'No broader reconciliation is pending.'}`);
            const unresolved = property(status, 'UnresolvedGroups', []) ?? [];
            query('#collectionTagSyncDiagnostics').innerHTML = unresolved.length === 0
                ? '<p>No unresolved enabled mapping groups.</p>'
                : unresolved.map(group => `
                    <div class="collectionTagSyncDiagnostic collectionTagSyncWarning">
                        <strong>Mapping group ${escapeHtml(Number(property(group, 'GroupIndex', 0)) + 1)}:</strong>
                        ${escapeHtml(property(group, 'TargetLabel', 'Unknown target'))} is skipped because
                        ${(property(group, 'MissingCollections', []) ?? []).map(collection =>
                            `${escapeHtml(property(collection, 'DisplayName', 'Missing collection'))}
                             (${escapeHtml(property(collection, 'Id', ''))})`).join(', ')} no longer resolves.
                    </div>`).join('');
        } catch {
            setStatus('#collectionTagSyncOperationalStatus', 'Operational status is unavailable.', 'Warning');
        }
    }

    function openCollectionCreation(select) {
        state.collectionCreationSession = { select, pending: false };
        const dialog = query('#collectionTagSyncCreateCollection');
        const input = query('#collectionTagSyncNewCollectionName');
        query('[data-action="create-collection"]').disabled = false;
        setStatus('#collectionTagSyncCreateCollectionStatus', '');
        input.value = '';
        if (!dialog.open) {
            dialog.showModal();
        }

        input.focus();
    }

    function closeCollectionCreation(session = state.collectionCreationSession) {
        if (!session || state.collectionCreationSession !== session) {
            return;
        }

        const prior = session.select;
        const dialog = query('#collectionTagSyncCreateCollection');
        if (dialog.open) {
            dialog.close();
        }

        state.collectionCreationSession = null;
        query('[data-action="create-collection"]').disabled = false;
        prior?.focus();
    }

    function selectCreatedCollection(entry, session = state.collectionCreationSession) {
        const id = String(property(entry, 'Id', '') || '');
        const displayName = String(property(entry, 'DisplayName', '') || '');
        const existingIndex = state.collections.findIndex(candidate =>
            String(property(candidate, 'Id', '')).toLowerCase() === id.toLowerCase());
        if (existingIndex >= 0) {
            state.collections[existingIndex] = entry;
        } else {
            state.collections.push(entry);
        }

        state.collections.sort((left, right) =>
            String(property(left, 'DisplayName', '')).localeCompare(
                String(property(right, 'DisplayName', '')), undefined, { sensitivity: 'base' })
            || String(property(left, 'Id', '')).localeCompare(String(property(right, 'Id', ''))));
        refreshCollectionSelects();
        if (state.collectionCreationSession !== session) {
            return;
        }

        if (session?.select) {
            session.select.value = id;
            const editor = session.select.closest('[data-node-editor]');
            if (editor) {
                editor.dataset.collectionDisplayName = displayName;
            }
            if (session.select.closest('#collectionTagSyncRunOnce')) {
                runOnceChanged(session.select);
            } else {
                updateMappingSummary(session.select);
                configurationChanged();
            }
        }

        closeCollectionCreation(session);
    }

    async function createCollection() {
        const session = state.collectionCreationSession;
        if (!session || session.pending) {
            return;
        }

        session.pending = true;
        query('[data-action="create-collection"]').disabled = true;
        const name = query('#collectionTagSyncNewCollectionName').value;
        setStatus('#collectionTagSyncCreateCollectionStatus', 'Creating collection…');
        try {
            const result = await requestJson(apiClient, 'POST', 'CollectionTagSync/Collections/Create', { Name: name });
            selectCreatedCollection(property(result, 'SelectedCollection', {}), session);
        } catch (error) {
            const result = responsePayload(error);
            const matches = property(result, 'MatchingCollections', []) ?? [];
            if (matches.length > 0) {
                for (const match of matches) {
                    const matchId = String(property(match, 'Id', '') || '');
                    const existingIndex = state.collections.findIndex(candidate =>
                        String(property(candidate, 'Id', '')).toLowerCase() === matchId.toLowerCase());
                    if (existingIndex >= 0) {
                        state.collections[existingIndex] = match;
                    } else {
                        state.collections.push(match);
                    }
                }

                refreshCollectionSelects();
                if (state.collectionCreationSession !== session) {
                    return;
                }

                const container = query('#collectionTagSyncCreateCollectionStatus');
                container.classList.add('collectionTagSyncWarning');
                container.innerHTML = '<p>A collection with this normalized name already exists. Select one:</p>'
                    + matches.map(match => `
                        <button is="emby-button" type="button" class="raised button"
                                data-action="select-existing-collection"
                                data-collection-id="${escapeHtml(property(match, 'Id', ''))}">
                            <span>${escapeHtml(property(match, 'DisplayName', ''))}
                                  — ${escapeHtml(property(match, 'Id', ''))}</span>
                        </button>`).join('');
                return;
            }

            if (state.collectionCreationSession !== session) {
                return;
            }

            setStatus(
                '#collectionTagSyncCreateCollectionStatus',
                property(result, 'Message', 'The server rejected the collection name.'),
                'Error');
        } finally {
            if (state.collectionCreationSession === session) {
                session.pending = false;
                query('[data-action="create-collection"]').disabled = false;
            }
        }
    }

    async function load() {
        if (state.loaded) {
            await Promise.all([refreshFullReconcileStatus(), refreshOperationalStatus()]);
            return;
        }

        dashboard.showLoadingMsg();
        try {
            const [configuration, collections, tags, runOnceGroups] = await Promise.all([
                apiClient.getPluginConfiguration(pluginId),
                requestJson(apiClient, 'GET', 'CollectionTagSync/Collections/Picker'),
                requestJson(apiClient, 'GET', 'CollectionTagSync/Tags/Picker'),
                requestJson(apiClient, 'GET', 'CollectionTagSync/RunOnce/Groups')
            ]);
            state.configuration = configuration;
            state.collections = collections ?? [];
            state.tags = tags ?? [];
            state.runOnceGroups = Array.isArray(runOnceGroups) ? runOnceGroups : [];
            renderTagChoices();
            renderMappingGroups();
            renderSettings();
            renderRunOnceGroups();
            state.loaded = true;
            await Promise.all([refreshFullReconcileStatus(), refreshOperationalStatus()]);
        } catch {
            setStatus(
                '#collectionTagSyncConfigurationStatus',
                'The administrator UI could not load its server data.',
                'Error');
        } finally {
            dashboard.hideLoadingMsg();
        }
    }

    view.addEventListener('click', async event => {
        const mappingSummary = event.target.closest('summary[data-action="edit-mapping"]');
        if (mappingSummary && view.contains(mappingSummary)) {
            event.preventDefault();
            const selectedGroup = mappingSummary.closest('[data-mapping-group]');
            const shouldOpen = !selectedGroup.open;
            for (const group of queryAll('[data-mapping-group]')) {
                group.open = false;
            }

            selectedGroup.open = shouldOpen;
            return;
        }

        const runOnceSummary = event.target.closest('summary[data-action="edit-run-once-group"]');
        if (runOnceSummary && view.contains(runOnceSummary)) {
            event.preventDefault();
            const selectedGroup = runOnceSummary.closest('[data-run-once-group]');
            const shouldOpen = !selectedGroup.open;
            for (const group of queryAll('[data-run-once-group]')) {
                group.open = false;
            }

            selectedGroup.open = shouldOpen;
            return;
        }

        const button = event.target.closest('button[data-action]');
        if (!button || !view.contains(button)) {
            return;
        }

        switch (button.dataset.action) {
            case 'add-mapping':
                addMappingGroup();
                break;
            case 'add-run-once-group':
                addRunOnceGroup();
                break;
            case 'remove-mapping':
                button.closest('[data-mapping-group]').remove();
                configurationChanged();
                break;
            case 'add-mapping-source': {
                const group = button.closest('[data-mapping-group]');
                addSource(group.querySelector('[data-role="sources"]'), 'mappingSource');
                updateMappingSummary(group);
                configurationChanged();
                break;
            }
            case 'remove-node': {
                const group = button.closest('[data-mapping-group]');
                const isRunOnce = Boolean(button.closest('#collectionTagSyncRunOnce'));
                button.closest('[data-node-editor]').remove();
                if (isRunOnce) {
                    runOnceChanged(button);
                } else {
                    updateMappingSummary(group);
                    configurationChanged();
                }
                break;
            }
            case 'save-configuration':
                await activateConfiguration();
                break;
            case 'preview-configuration':
                await previewConfiguration();
                break;
            case 'confirm-configuration':
                await confirmConfiguration();
                break;
            case 'add-run-once-source':
                addSource(
                    button.closest('[data-run-once-group]').querySelector('[data-role="run-once-sources"]'),
                    'runOnceSource');
                runOnceChanged(button);
                break;
            case 'save-run-once-group':
                await saveRunOnceGroup(button.closest('[data-run-once-group]'));
                break;
            case 'preview-run-once':
                await previewRunOnce(button.closest('[data-run-once-group]'));
                break;
            case 'delete-run-once-group':
                await deleteRunOnceGroup(button.closest('[data-run-once-group]'));
                break;
            case 'confirm-run-once':
                await confirmRunOnce();
                break;
            case 'queue-full-reconcile':
                await queueFullReconcile();
                break;
            case 'refresh-status':
                await Promise.all([refreshFullReconcileStatus(), refreshOperationalStatus()]);
                break;
            case 'confirm-full-reconcile':
                await confirmFullReconcile();
                break;
            case 'create-collection':
                await createCollection();
                break;
            case 'cancel-create-collection':
                closeCollectionCreation();
                break;
            case 'select-existing-collection': {
                const id = button.dataset.collectionId;
                const entry = state.collections.find(candidate =>
                    String(property(candidate, 'Id', '')).toLowerCase() === String(id).toLowerCase());
                if (entry) {
                    selectCreatedCollection(entry);
                }
                break;
            }
        }
    });

    view.addEventListener('change', event => {
        const target = event.target;
        if (target.matches('[data-field="node-kind"]')) {
            updateNodeVisibility(target.closest('[data-node-editor]'));
        }

        if (target.matches('[data-field="collection-id"]') && target.value === addNewCollectionValue) {
            target.value = target.dataset.previousValue ?? '';
            openCollectionCreation(target);
            return;
        }

        if (target.matches('[data-run-once-exclusion]')) {
            const itemId = target.dataset.runOnceExclusion;
            if (target.checked) {
                state.runOnceExcludedIds.add(itemId);
            } else {
                state.runOnceExcludedIds.delete(itemId);
            }
            runOnceChanged(null, false);
            return;
        }

        if (target.id === 'collectionTagSyncCircuitBreakerEnabled') {
            updateCircuitBreakerWarning();
        }

        if (target.closest('#collectionTagSyncRunOnce')) {
            runOnceChanged(target);
        } else if (target.closest('#collectionTagSyncMappings')
            || target.closest('#collectionTagSyncReconciliationSafety')) {
            if (target.closest('#collectionTagSyncMappings')) {
                updateMappingSummary(target);
            }

            configurationChanged();
        }
    });

    view.addEventListener('focusin', event => {
        if (event.target.matches('[data-field="collection-id"]')
            && event.target.value !== addNewCollectionValue) {
            event.target.dataset.previousValue = event.target.value;
        }
    });

    view.addEventListener('input', event => {
        if (event.target.matches('[data-run-once-exclusion]')) {
            return;
        }

        if (event.target.closest('#collectionTagSyncRunOnce')) {
            runOnceChanged(event.target);
        } else if (event.target.closest('#collectionTagSyncMappings')
            || event.target.closest('#collectionTagSyncReconciliationSafety')) {
            if (event.target.closest('#collectionTagSyncMappings')) {
                updateMappingSummary(event.target);
            }

            configurationChanged();
        }
    });

    query('#collectionTagSyncCreateCollection').addEventListener('cancel', event => {
        event.preventDefault();
        closeCollectionCreation();
    });

    view.addEventListener('viewshow', load);
    view.addEventListener('viewhide', () => {
        for (const timer of [...state.timers]) {
            clearTimer(timer);
        }
    });
}
