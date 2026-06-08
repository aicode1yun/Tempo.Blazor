import { createCanvasDocumentModel, normalizePageSettings } from './canvas-document-model.mjs';

export { createCanvasDocumentModel, normalizePageSettings };

export function createModelStore(initialModel) {
    let model = createCanvasDocumentModel(initialModel);
    let version = readModelVersion(model, 0);

    function getModel() {
        return model;
    }

    function setModel(nextModel, options = {}) {
        model = options.normalize === false ? nextModel : createCanvasDocumentModel(nextModel);
        version = readModelVersion(model, version + 1);
        return model;
    }

    function getVersion() {
        return version;
    }

    return {
        getModel,
        setModel,
        getVersion,
    };
}

function readModelVersion(model, fallback) {
    const value = Number(model?.version);
    return Number.isFinite(value) ? value : fallback;
}

export function normalizeCanvasDocumentModel(input) {
    return createCanvasDocumentModel(input);
}
