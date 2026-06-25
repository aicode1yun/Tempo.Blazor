// Renders a signing field box on the canvas content layer (plan S2.7/S2.8). The box is a tinted,
// role-coloured rectangle with a type icon, the field label, and a required marker. A focus ring is
// drawn when the field is selected. The renderer is engine-agnostic: it only reads the command (the
// display list resolves the role colour + label; this module never localises text).

const TYPE_ICONS = Object.freeze({
    signature: '✒', initials: '✎', date: '📅', dateNow: '📅', number: '#', image: '🖼', file: '📎',
    select: '▾', checkbox: '☐', multiple: '☑', radio: '◉', cells: '▦', stamp: '◍', payment: '💳',
    phone: '📞', verification: '🛡', kba: '🔑', text: '✦',
});

export function paintSigningField(context, command, options = {}) {
    if (!context || typeof context.fillRect !== 'function') {
        return false;
    }

    const x = Number(command.x) || 0;
    const y = Number(command.y) || 0;
    const width = Math.max(1, Number(command.width) || 1);
    const height = Math.max(1, Number(command.height) || 1);
    const roleColor = /^#[0-9a-f]{6}$/i.test(String(command.roleColor || '')) ? command.roleColor : '#2563eb';

    context.save?.();

    // Tinted fill so the field reads as an interactive region without obscuring page content beneath.
    context.fillStyle = withAlpha(roleColor, 0.12);
    context.fillRect(x, y, width, height);

    // Role-coloured border.
    context.strokeStyle = roleColor;
    context.lineWidth = 1.5;
    context.strokeRect(x + 0.75, y + 0.75, width - 1.5, height - 1.5);

    // Selected focus ring (an extra outset stroke).
    if (command.selected === true) {
        context.strokeStyle = roleColor;
        context.lineWidth = 1;
        context.strokeRect(x - 1.5, y - 1.5, width + 3, height + 3);
    }

    // Icon + label + required marker, vertically centred, clipped within the box.
    const fontSize = Math.max(8, Math.min(14, height - 8));
    context.fillStyle = roleColor;
    context.font = `${fontSize}px sans-serif`;
    context.textBaseline = 'middle';
    const icon = TYPE_ICONS[String(command.fieldType || 'text')] || TYPE_ICONS.text;
    const label = String(command.label || '').trim();
    const required = command.required === true ? ' *' : '';
    const text = `${icon} ${label || defaultLabel(command.fieldType)}${required}`;
    context.fillText(text, x + 6, y + height / 2);

    context.restore?.();
    return true;
}

function defaultLabel(fieldType) {
    const type = String(fieldType || 'text');
    return type.charAt(0).toUpperCase() + type.slice(1);
}

function withAlpha(hex, alpha) {
    const value = String(hex || '').replace('#', '');
    const r = parseInt(value.slice(0, 2), 16) || 0;
    const g = parseInt(value.slice(2, 4), 16) || 0;
    const b = parseInt(value.slice(4, 6), 16) || 0;
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
