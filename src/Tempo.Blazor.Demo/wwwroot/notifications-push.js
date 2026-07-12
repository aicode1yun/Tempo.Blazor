// Web Push subscription helper for the notifications demo (ES module).

export function isSupported() {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

export async function ensurePermission() {
    if (!('Notification' in window)) return 'unsupported';
    if (Notification.permission === 'granted') return 'granted';
    if (Notification.permission === 'denied') return 'denied';
    return await Notification.requestPermission();
}

export async function subscribe(userId, apiBase, vapidPublicKey) {
    let subscription = null;
    try {
        const reg = await navigator.serviceWorker.register('/push-sw.js');
        await navigator.serviceWorker.ready;
        subscription = await reg.pushManager.getSubscription();
        if (!subscription) {
            subscription = await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
            });
        }
    } catch (e) {
        // Push service unavailable (offline/headless CI) — fall back to a synthetic subscription
        // so the store + server-send pipeline is still demonstrated end to end.
        subscription = null;
    }

    const dto = subscription ? toDto(subscription, userId) : synthetic(userId, vapidPublicKey);
    const resp = await fetch(apiBase + '/api/notifications/push/subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(dto)
    });
    return { ok: resp.ok, synthetic: subscription === null, endpoint: dto.endpoint };
}

function toDto(sub, userId) {
    return {
        userId,
        endpoint: sub.endpoint,
        p256dh: encodeKey(sub.getKey('p256dh')),
        auth: encodeKey(sub.getKey('auth')),
        expirationTime: sub.expirationTime ? new Date(sub.expirationTime).toISOString() : null
    };
}

function synthetic(userId, vapidPublicKey) {
    const rnd = Math.random().toString(36).slice(2) + Date.now().toString(36);
    return {
        userId,
        endpoint: 'https://push.example.invalid/' + rnd,
        // A real (valid-format) P-256 public point + auth so the server can attempt a send.
        p256dh: vapidPublicKey,
        auth: 'aUExampleAuthSecret0123',
        expirationTime: null
    };
}

function encodeKey(buffer) {
    if (!buffer) return '';
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function urlBase64ToUint8Array(base64) {
    const padding = '='.repeat((4 - base64.length % 4) % 4);
    const b64 = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(b64);
    const arr = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) arr[i] = raw.charCodeAt(i);
    return arr;
}
