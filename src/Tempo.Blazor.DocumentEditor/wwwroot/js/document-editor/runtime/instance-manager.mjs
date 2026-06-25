// Phase D — runtime/instance-manager.mjs
// Thin wrapper around the `Map<instanceId, instance>` that the legacy engine keeps as
// the `_instances` closure variable. Extracted so future module migrations can
// register/lookup engine instances through a clean API instead of reaching into IIFE
// closure state.

export class InstanceManager {
    constructor() {
        this.instances = new Map();
    }

    // Register a new instance. Returns the instance for chaining.
    register(instanceId, instance) {
        if (!instanceId) throw new TypeError('InstanceManager.register requires an instanceId');
        if (!instance) throw new TypeError('InstanceManager.register requires an instance');
        this.instances.set(String(instanceId), instance);
        return instance;
    }

    // Lookup an instance by id. Returns null if not registered.
    get(instanceId) {
        if (!instanceId) return null;
        return this.instances.get(String(instanceId)) || null;
    }

    has(instanceId) {
        if (!instanceId) return false;
        return this.instances.has(String(instanceId));
    }

    // Remove an instance. Returns true if a record was actually removed.
    remove(instanceId) {
        if (!instanceId) return false;
        return this.instances.delete(String(instanceId));
    }

    // Iterate over all `[id, instance]` pairs.
    entries() {
        return this.instances.entries();
    }

    keys() {
        return this.instances.keys();
    }

    values() {
        return this.instances.values();
    }

    get size() {
        return this.instances.size;
    }

    clear() {
        this.instances.clear();
    }
}

// Default global manager. The legacy IIFE has exactly one Map shared across the whole
// page; this single instance preserves the same semantics for callers that don't want
// to manage their own.
export const defaultInstanceManager = new InstanceManager();
