// Phase D — history/operation-types.mjs
// Enumerations for operation kinds and transaction kinds used by the history/undo stack.
// Extracted verbatim from the legacy IIFE so that the bundled engine and the legacy
// document-editor-wysiwyg.js agree on the exact same string values.

export const OperationTypes = Object.freeze({
    InsertText: 'InsertText',
    DeleteRange: 'DeleteRange',
    SplitParagraph: 'SplitParagraph',
    MergeParagraph: 'MergeParagraph',
    ApplyMark: 'ApplyMark',
    RemoveMark: 'RemoveMark',
    SetParagraphAttribute: 'SetParagraphAttribute',
    InsertImage: 'InsertImage',
    UpdateImageLayout: 'UpdateImageLayout',
    MoveDrawingObject: 'MoveDrawingObject',
    UpdateImageMetadata: 'UpdateImageMetadata',
    InsertTable: 'InsertTable',
    UpdateTableCell: 'UpdateTableCell',
    AcceptRevision: 'AcceptRevision',
    RejectRevision: 'RejectRevision',
    SetSelection: 'SetSelection',
    RestoreSnapshot: 'RestoreSnapshot',
});

export const TransactionTypes = Object.freeze({
    Default: 'default',
    Typing: 'typing',
    Undo: 'undo',
    Redo: 'redo',
    Preview: 'preview',
    Remote: 'remote',
});

// Mirrors the legacy `isTypingLikeTransactionType` helper. Used by autosave gates and
// snapshot merge logic to decide whether two consecutive transactions should collapse.
export function isTypingLikeTransactionType(value) {
    const type = String(value || '').toLowerCase();
    return type === TransactionTypes.Typing
        || type === 'typing'
        || type === 'delete'
        || type === 'keyboarddelete';
}
