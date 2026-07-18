import { buildWordListChecker } from '../../document-editor/core-engine/core-editor.mjs';
import { createCanvasRunText, normalizeMarkType, orderedCanvasBlocks } from '../layout/canvas-text-style.mjs';

const WORD_PATTERN = /[\p{L}\p{M}][\p{L}\p{M}'’-]*/gu;
const TEXT_BLOCK_TYPES = new Set(['paragraph', 'heading', 'list', 'quote']);

export function createCanvasProofingService(options = {}) {
    let config = normalizeProofingOptions(options);
    let ignoredInstances = new Set();
    let ignoredWords = new Set();
    let diagnostics = [];
    let revision = 0;
    let lastModelVersion = null;

    function analyze(model, render = null) {
        const version = Number(model?.version ?? model?.Version ?? 0) || 0;
        const dirtyBlockIds = new Set([
            ...(render?.incremental?.dirtyBlockIds || []),
            ...(render?.dirtyBlockIds || []),
            ...(render?.result?.dirtyBlockIds || []),
        ].map(value => String(value || '')).filter(Boolean));
        if (lastModelVersion === version && dirtyBlockIds.size === 0) {
            return snapshot();
        }

        const canUpdateIncrementally = config.incremental !== false
            && lastModelVersion != null
            && dirtyBlockIds.size > 0;
        lastModelVersion = version;
        if (!config.enabled) {
            diagnostics = [];
        } else if (canUpdateIncrementally) {
            const nextDirtyDiagnostics = collectDiagnostics(model, config, ignoredInstances, ignoredWords, { blockIds: dirtyBlockIds });
            diagnostics = sortDiagnostics(model, [
                ...diagnostics.filter(item => !dirtyBlockIds.has(String(item?.blockId || ''))),
                ...nextDirtyDiagnostics,
            ]);
        } else {
            diagnostics = collectDiagnostics(model, config, ignoredInstances, ignoredWords);
        }

        revision += 1;
        return snapshot();
    }

    function setOptions(nextOptions = {}) {
        config = normalizeProofingOptions({ ...config, ...nextOptions });
        diagnostics = [];
        revision += 1;
        lastModelVersion = null;
        return snapshot();
    }

    function diagnosticAtPosition(blockId, offset) {
        const id = String(blockId || '');
        const currentOffset = Number(offset || 0) || 0;
        return diagnostics.find(item =>
            String(item.blockId || '') === id
            && currentOffset >= Number(item.start || 0)
            && currentOffset <= Number(item.end || 0)) || null;
    }

    function ignoreOnce(diagnostic) {
        const key = diagnosticKey(diagnostic);
        if (key) {
            ignoredInstances.add(key);
            diagnostics = diagnostics.filter(item => diagnosticKey(item) !== key);
            revision += 1;
        }

        return snapshot();
    }

    function ignoreAll(word) {
        const normalized = normalizeWord(word);
        if (normalized) {
            ignoredWords.add(normalized);
            diagnostics = diagnostics.filter(item => normalizeWord(item.word) !== normalized);
            revision += 1;
        }

        return snapshot();
    }

    function addToDictionary(word) {
        const normalized = normalizeWord(word);
        if (normalized) {
            config.addToDictionary?.(word);
            config.flaggedWords.delete(normalized);
            ignoredWords.add(normalized);
            diagnostics = diagnostics.filter(item => normalizeWord(item.word) !== normalized);
            revision += 1;
        }

        return snapshot();
    }

    function snapshot() {
        return {
            enabled: config.enabled,
            revision,
            diagnostics: diagnostics.map(cloneDiagnostic),
            diagnosticCount: diagnostics.length,
            ignoredWordCount: ignoredWords.size,
            defaultLanguage: config.defaultLanguage,
        };
    }

    return {
        analyze,
        setOptions,
        diagnosticAtPosition,
        ignoreOnce,
        ignoreAll,
        addToDictionary,
        snapshot,
    };
}

export function collectDiagnostics(model, config = normalizeProofingOptions(), ignoredInstances = new Set(), ignoredWords = new Set(), options = {}) {
    const diagnostics = [];
    const blocks = orderedCanvasBlocks(model);
    const defaultLanguage = config.defaultLanguage || model?.metadata?.language || model?.metadata?.culture || '';
    const protectedDocument = model?.isProtected === true || model?.IsProtected === true;
    const readonlyDocument = model?.readOnly === true || model?.readonly === true || model?.ReadOnly === true;
    const allowedBlockIds = options?.blockIds instanceof Set
        ? options.blockIds
        : Array.isArray(options?.blockIds)
            ? new Set(options.blockIds.map(value => String(value || '')).filter(Boolean))
            : null;

    for (const block of blocks) {
        if (allowedBlockIds && !allowedBlockIds.has(String(block?.id || ''))) {
            continue;
        }

        if (!isProofableBlock(block)) {
            continue;
        }

        const spans = proofingSpansForBlock(block, defaultLanguage, model);
        for (const span of spans) {
            if (!span.text || !languageEnabled(config, span.language)) {
                continue;
            }

            WORD_PATTERN.lastIndex = 0;
            for (const match of span.text.matchAll(WORD_PATTERN)) {
                const word = match[0];
                const normalized = normalizeWord(word);
                if (!normalized || ignoredWords.has(normalized)) {
                    continue;
                }

                const start = span.start + (match.index || 0);
                const end = start + word.length;
                const context = {
                    model,
                    block,
                    blockId: String(block.id || ''),
                    start,
                    end,
                    language: span.language,
                    commentIds: span.commentIds,
                    revisionIds: span.revisionIds,
                };
                if (!isMisspelled(config, word, context)) {
                    continue;
                }

                const readonlyReason = readOnlyReason({
                    readonlyDocument,
                    protectedDocument,
                    block,
                    span,
                });
                const diagnostic = {
                    id: `spell:${block.id}:${start}:${end}:${normalized}`,
                    type: 'spelling',
                    blockId: String(block.id || ''),
                    start,
                    end,
                    word,
                    language: span.language,
                    suggestions: suggestionsForWord(config, word, context),
                    commentIds: span.commentIds.slice(),
                    revisionIds: span.revisionIds.slice(),
                    canApplyFix: !readonlyReason,
                    readonlyReason,
                };

                if (!ignoredInstances.has(diagnosticKey(diagnostic))) {
                    diagnostics.push(diagnostic);
                }
            }
        }
    }

    return sortDiagnostics(model, diagnostics);
}

export function mapDiagnosticRects(diagnostic, textRects = []) {
    const blockId = String(diagnostic?.blockId || '');
    const start = Number(diagnostic?.start || 0) || 0;
    const end = Number(diagnostic?.end || 0) || 0;
    const rects = [];

    for (const item of textRects || []) {
        if (String(item?.blockId || '') !== blockId) {
            continue;
        }

        const rectStart = Number(item.start ?? item.startOffset ?? 0) || 0;
        const rectEnd = Number(item.end ?? item.endOffset ?? 0) || 0;
        if (rectEnd <= start || rectStart >= end) {
            continue;
        }

        const textLength = Math.max(1, rectEnd - rectStart);
        const leftRatio = Math.max(0, (Math.max(start, rectStart) - rectStart) / textLength);
        const rightRatio = Math.min(1, (Math.min(end, rectEnd) - rectStart) / textLength);
        const width = Math.max(1, Number(item.width || 0) || 0);
        rects.push({
            pageIndex: Number(item.pageIndex || 0) || 0,
            x: (Number(item.x || 0) || 0) + width * leftRatio,
            y: Number(item.y || 0) || 0,
            width: Math.max(1, width * Math.max(0, rightRatio - leftRatio)),
            height: Math.max(1, Number(item.height || 0) || 16),
            baseline: Number(item.baseline || 0) || 0,
            blockId,
            start: Math.max(start, rectStart),
            end: Math.min(end, rectEnd),
        });
    }

    return rects;
}

export function diagnosticKey(diagnostic) {
    const blockId = String(diagnostic?.blockId || '');
    const start = Number(diagnostic?.start || 0) || 0;
    const end = Number(diagnostic?.end || 0) || 0;
    const word = normalizeWord(diagnostic?.word || '');
    return blockId && word ? `${blockId}:${start}:${end}:${word}` : '';
}

function normalizeProofingOptions(options = {}) {
    // setOptions merges the ALREADY-normalized config (Sets/Maps) under incoming plain-JSON
    // options, so every list/map input here must tolerate both shapes.
    const suggestions = new Map();
    const rawSuggestions = toSuggestionEntries(options.suggestions || options.Suggestions);
    for (const [word, values] of rawSuggestions) {
        const key = normalizeWord(word);
        const list = Array.isArray(values)
            ? values.map(value => String(value || '').trim()).filter(Boolean)
            : [];
        if (key && list.length) {
            suggestions.set(key, list);
        }
    }

    const flaggedWords = toWordArray(options.flaggedWords || options.FlaggedWords)
        .map(normalizeWord)
        .filter(Boolean);
    const knownWords = toWordArray(options.knownWords || options.KnownWords || options.known || options.Known)
        .map(normalizeWord)
        .filter(Boolean);
    const checker = normalizeChecker(options, flaggedWords, knownWords, Object.fromEntries(rawSuggestions));
    const suggestionProvider = normalizeSuggestionProvider(options, checker);
    const languages = new Set(toWordArray(options.languages || options.Languages || options.enabledLanguages || options.EnabledLanguages)
        .map(value => String(value || '').trim())
        .filter(Boolean));

    return {
        enabled: options.enabled !== false && options.Enabled !== false,
        defaultLanguage: String(options.defaultLanguage || options.DefaultLanguage || '').trim(),
        flaggedWords: new Set(flaggedWords),
        knownWords: new Set(knownWords),
        suggestions,
        checker,
        suggestionProvider,
        addToDictionary: normalizeAddToDictionary(options, checker, suggestionProvider),
        languages,
        incremental: options.incremental !== false && options.Incremental !== false,
    };
}

function toWordArray(value) {
    if (Array.isArray(value)) {
        return value;
    }

    if (value instanceof Set) {
        return [...value];
    }

    return [];
}

function toSuggestionEntries(value) {
    if (value instanceof Map) {
        return [...value.entries()];
    }

    if (value && typeof value === 'object') {
        return Object.entries(value);
    }

    return [];
}

// A wrapper the service derived from an auto-built checker must be rebuilt (not kept) on a
// setOptions re-merge, otherwise "add to dictionary" would keep feeding the stale checker.
function normalizeAddToDictionary(options, checker, suggestionProvider) {
    if (typeof options.addToDictionary === 'function' && options.addToDictionary.__tmDerived !== true) {
        return options.addToDictionary;
    }

    if (typeof checker?.addToDictionary === 'function') {
        const derived = word => checker.addToDictionary(word);
        derived.__tmDerived = true;
        return derived;
    }

    if (typeof suggestionProvider?.addToDictionary === 'function') {
        const derived = word => suggestionProvider.addToDictionary(word);
        derived.__tmDerived = true;
        return derived;
    }

    return null;
}

function isProofableBlock(block) {
    return block
        && TEXT_BLOCK_TYPES.has(String(block.type || '').toLowerCase())
        && Array.isArray(block?.content?.runs);
}

function blockTextFromRuns(block) {
    return (block?.content?.runs || []).map(run => createCanvasRunText(run)).join('');
}

function suggestionsForWord(config, word, context = {}) {
    const providerSuggestions = callSuggestions(config.suggestionProvider, word, context)
        || callSuggestions(config.checker, word, context);
    const suggestions = providerSuggestions
        || config.suggestions.get(normalizeWord(word))
        || [];
    return suggestions.map(value => String(value || '').trim()).filter(Boolean).slice(0, 6);
}

function normalizeWord(word) {
    return String(word || '').trim().toLocaleLowerCase();
}

function cloneDiagnostic(diagnostic) {
    return {
        ...diagnostic,
        suggestions: Array.isArray(diagnostic?.suggestions) ? diagnostic.suggestions.slice() : [],
        commentIds: Array.isArray(diagnostic?.commentIds) ? diagnostic.commentIds.slice() : [],
        revisionIds: Array.isArray(diagnostic?.revisionIds) ? diagnostic.revisionIds.slice() : [],
    };
}

function normalizeChecker(options, flaggedWords, knownWords, rawSuggestions) {
    const explicit = options.checker || options.Checker || options.wordChecker || options.WordChecker || options.provider || options.Provider;
    // A checker the service itself built from word lists must NOT survive a setOptions re-merge —
    // it would shadow the refreshed word lists. Host-supplied checkers are kept.
    if (explicit && typeof explicit === 'object' && explicit.__tmWordListChecker !== true) {
        return explicit;
    }

    if (flaggedWords.length > 0 || knownWords.length > 0 || Object.keys(rawSuggestions || {}).length > 0) {
        const checker = buildWordListChecker({
            flagged: flaggedWords,
            known: knownWords.length > 0 ? knownWords : undefined,
            suggestions: rawSuggestions,
        });
        checker.__tmWordListChecker = true;
        return checker;
    }

    return null;
}

function normalizeSuggestionProvider(options, checker) {
    const provider = options.suggestionProvider || options.SuggestionProvider || options.suggestionsProvider || options.SuggestionsProvider;
    // Same rule as normalizeChecker: an auto-built word-list checker acting as the suggestion
    // provider must be rebuilt on a setOptions re-merge, not carried over.
    if (provider && typeof provider === 'object' && provider.__tmWordListChecker !== true) {
        return provider;
    }

    return checker && typeof checker.suggest === 'function' ? checker : null;
}

function isMisspelled(config, word, context) {
    const checker = config.checker;
    if (checker && typeof checker.isMisspelled === 'function') {
        return checker.isMisspelled(word, context) === true;
    }

    if (checker && typeof checker.check === 'function') {
        const result = checker.check(word, context);
        return result === true || result?.misspelled === true || result?.isMisspelled === true;
    }

    return config.flaggedWords.has(normalizeWord(word));
}

function callSuggestions(provider, word, context) {
    if (!provider || typeof provider.suggest !== 'function') {
        return null;
    }

    const result = provider.suggest(word, context);
    return Array.isArray(result) ? result : null;
}

function proofingSpansForBlock(block, defaultLanguage, model) {
    const spans = [];
    let offset = 0;
    for (const run of block?.content?.runs || []) {
        const text = createCanvasRunText(run);
        const start = offset;
        offset += text.length;
        if (!text || shouldSkipRun(run)) {
            continue;
        }

        const span = {
            blockId: String(block?.id || ''),
            start,
            text,
            language: runLanguage(run, block, defaultLanguage, model),
            readOnly: isReadOnly(run) || isProtected(run),
            commentIds: commentIdsFromRun(run),
            revisionIds: revisionIdsFromRun(run),
        };
        appendSpan(spans, span);
    }

    return spans;
}

function appendSpan(spans, span) {
    const previous = spans[spans.length - 1];
    if (previous
        && previous.start + previous.text.length === span.start
        && previous.language === span.language
        && previous.readOnly === span.readOnly
        && arrayKey(previous.commentIds) === arrayKey(span.commentIds)
        && arrayKey(previous.revisionIds) === arrayKey(span.revisionIds)) {
        previous.text += span.text;
        return;
    }

    spans.push(span);
}

function shouldSkipRun(run) {
    if (run?.spellcheck === false || run?.spellCheck === false || run?.preserve?.spellcheck === false || run?.preserve?.spellCheck === false) {
        return true;
    }

    const language = String(run?.language || run?.Language || run?.preserve?.language || run?.preserve?.Language || '').trim().toLowerCase();
    if (language === 'zxx' || language === 'none') {
        return true;
    }

    return (run?.marks || []).some(mark => {
        const type = normalizeMarkType(mark?.type);
        const value = normalizeMarkType(mark?.value || mark?.Value || mark?.revisionType || mark?.RevisionType);
        return type === 'noproof'
            || type === 'spellcheckdisabled'
            || (type === 'revision' && (value === 'deletion' || value === 'delete' || value === 'removed'));
    });
}

function runLanguage(run, block, defaultLanguage, model) {
    return String(
        run?.language
        || run?.Language
        || run?.preserve?.language
        || run?.preserve?.Language
        || block?.language
        || block?.Language
        || block?.preserve?.language
        || block?.preserve?.Language
        || defaultLanguage
        || model?.metadata?.language
        || model?.metadata?.culture
        || '').trim();
}

function languageEnabled(config, language) {
    return config.languages.size === 0 || config.languages.has(String(language || '').trim());
}

function readOnlyReason({ readonlyDocument, protectedDocument, block, span }) {
    if (readonlyDocument) {
        return 'readonly';
    }

    if (protectedDocument || isProtected(block) || span.readOnly) {
        return 'protected';
    }

    if (isReadOnly(block)) {
        return 'readonly';
    }

    return '';
}

function isReadOnly(value) {
    return value?.readOnly === true || value?.readonly === true || value?.ReadOnly === true;
}

function isProtected(value) {
    return value?.isProtected === true || value?.IsProtected === true || value?.protected === true || value?.Protected === true;
}

function commentIdsFromRun(run) {
    return unique((run?.marks || [])
        .filter(mark => normalizeMarkType(mark?.type) === 'commentanchor' || normalizeMarkType(mark?.type) === 'comment')
        .map(mark => mark?.commentAnchor?.commentId || mark?.commentId || mark?.value || mark?.Value));
}

function revisionIdsFromRun(run) {
    return unique((run?.marks || [])
        .filter(mark => normalizeMarkType(mark?.type) === 'revision')
        .map(mark => mark?.revisionId || mark?.RevisionId || mark?.value || mark?.Value));
}

function sortDiagnostics(model, items) {
    const order = new Map(orderedCanvasBlocks(model).map((block, index) => [String(block?.id || ''), index]));
    return items
        .map(cloneDiagnostic)
        .sort((left, right) => {
            const blockOrder = (order.get(String(left.blockId || '')) ?? Number.MAX_SAFE_INTEGER)
                - (order.get(String(right.blockId || '')) ?? Number.MAX_SAFE_INTEGER);
            return blockOrder !== 0
                ? blockOrder
                : (Number(left.start || 0) || 0) - (Number(right.start || 0) || 0);
        });
}

function arrayKey(values) {
    return (values || []).join('\u0001');
}

function unique(values) {
    return [...new Set(values.map(value => String(value || '').trim()).filter(Boolean))];
}
