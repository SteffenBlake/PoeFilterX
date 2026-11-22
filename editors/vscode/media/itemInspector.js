/* global acquireVsCodeApi */
(() => {
    const vscode = acquireVsCodeApi();
    const byId = id => document.getElementById(id);
    let currentInput = vscode.getState();
    let fields = [];
    let latest;
    function gameColor(color) {
        const red = parseInt(color.slice(1, 3), 16);
        const green = parseInt(color.slice(3, 5), 16);
        const blue = parseInt(color.slice(5, 7), 16);
        const alpha = 0.4 + 0.6 * parseInt(color.slice(7, 9), 16) / 255;
        return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
    }
    function scaleLabel(label, filterSize) {
        // Calibrate against the previous one-line rectangle heights: size 1 at 2x,
        // size 45 at 0.7x. Scale a single reference label, including its padding and border.
        const minimumSize = 1;
        const referenceSize = 45;
        label.style.fontSize = `${referenceSize}px`;
        const metrics = window.getComputedStyle(label);
        const lineHeight = parseFloat(metrics.lineHeight);
        const insets = parseFloat(metrics.paddingTop) + parseFloat(metrics.paddingBottom)
            + parseFloat(metrics.borderTopWidth) + parseFloat(metrics.borderBottomWidth);
        const referenceHeight = lineHeight + insets;
        const minimumHeight = (lineHeight * minimumSize / referenceSize + insets) * 2;
        const maximumHeight = referenceHeight * 0.7;
        const position = (Math.max(minimumSize, Math.min(referenceSize, filterSize)) - minimumSize)
            / (referenceSize - minimumSize);
        const height = minimumHeight + (maximumHeight - minimumHeight) * position;
        label.style.zoom = String(height / referenceHeight);
    }
    function renderIcon(icon, hidden) {
        const colors = ['Red', 'Green', 'Blue', 'Brown', 'White', 'Yellow', 'Cyan', 'Grey', 'Orange', 'Pink', 'Purple'];
        const shapes = ['Circle', 'Diamond', 'Hexagon', 'Square', 'Star', 'Triangle', 'Cross', 'Moon', 'Raindrop', 'Kite', 'Pentagon', 'UpsideDownHouse'];
        const sizes = ['Large', 'Medium', 'Small'];
        const visible = !hidden && icon && icon.enabled !== false && colors.includes(icon.color) && shapes.includes(icon.shape) && [0, 1, 2].includes(icon.size);
        const image = byId('preview-icon');
        image.hidden = !visible;
        if (!visible) { return; }
        image.src = `${image.dataset.root}/LootFilter${sizes[icon.size]}${icon.color}${icon.shape}.png`;
        image.alt = `${icon.color} ${icon.shape}`;
    }
    function gameValue() {
        return document.querySelector('#game input:checked').value;
    }
    function input() {
        const facts = Object.create(null);
        for (const field of fields) {
            const control = byId(`fact-${field.name}`);
            const value = control.multiple ? Array.from(control.selectedOptions, option => option.value).join('|') : control.value.trim();
            if (value) { facts[field.name] = value; }
        }
        return { item: byId('item').value, game: gameValue(), areaLevel: Number(byId('area').value), facts };
    }
    function applyInput(value) {
        currentInput = value;
        byId('item').value = value.item;
        document.querySelectorAll('#game input').forEach(control => { control.checked = control.value === value.game; });
        byId('area').value = value.areaLevel;
        for (const field of fields) {
            const control = byId(`fact-${field.name}`);
            const fact = value.facts?.[field.name] ?? '';
            if (control.multiple) {
                Array.from(control.options).forEach(option => { option.selected = fact.split('|').includes(option.value); });
            } else { control.value = fact; }
        }
    }
    function element(tag, text = '', className) {
        const node = document.createElement(tag);
        node.textContent = text;
        if (className) { node.className = className; }
        return node;
    }
    function renderFields(definitions) {
        currentInput = fields.length ? input() : currentInput;
        fields = definitions;
        const container = byId('facts');
        container.replaceChildren();
        for (const field of fields) {
            const label = element('label', field.name.replace(/([a-z])([A-Z])/g, '$1 $2'));
            label.htmlFor = `fact-${field.name}`;
            let control;
            if (field.kind === 'boolean' || field.choices.length) {
                control = element('select');
                if (field.name === 'HasInfluence') {
                    control.multiple = true;
                    control.size = field.choices.length;
                    control.title = 'Leave unselected to use the copied item. Ctrl/Cmd-click to select multiple influences.';
                } else {
                    const automatic = element('option', 'From item');
                    automatic.value = '';
                    control.append(automatic);
                }
                for (const value of field.kind === 'boolean' ? ['True', 'False'] : field.choices) {
                    const option = element('option', value);
                    option.value = value;
                    control.append(option);
                }
            } else {
                control = element('input');
                control.type = field.kind === 'number' ? 'number' : 'text';
                control.placeholder = 'From item';
                if (field.kind === 'number') {
                    control.step = '1';
                    control.min = field.minimum;
                    control.max = field.maximum;
                }
                if (field.kind === 'text') { control.title = 'Use | between multiple values.'; }
            }
            control.id = label.htmlFor;
            container.append(label, control);
        }
        if (currentInput) { applyInput(currentInput); }
    }
    function check() {
        const value = input();
        vscode.setState(value);
        byId('status').textContent = 'Checking…';
        byId('check').disabled = true;
        vscode.postMessage({ type: 'check', input: value });
    }
    byId('check-form').addEventListener('submit', event => { event.preventDefault(); check(); });
    byId('clear').addEventListener('click', () => {
        applyInput({ item: '', game: gameValue(), areaLevel: gameValue() === 'Poe1' ? 83 : 80, facts: {} });
        vscode.setState(input());
        latest = undefined;
        byId('results').replaceChildren();
        byId('errors').replaceChildren();
        byId('preview-section').hidden = true;
        byId('status').textContent = '';
        byId('check').disabled = false;
        vscode.postMessage({ type: 'clear' });
        byId('item').focus();
    });
    byId('game').addEventListener('change', () => {
        if ([80, 83].includes(Number(byId('area').value))) { byId('area').value = gameValue() === 'Poe1' ? 83 : 80; }
        if (byId('item').value) { check(); }
    });
    byId('area').addEventListener('change', () => { if (byId('item').value && byId('area').checkValidity()) { check(); } });
    function source(location) {
        const button = element('button', `${location.path.split(/[/\\]/).pop()}:${location.line}`, 'source');
        button.title = `${location.path}:${location.line}:${location.column}`;
        button.addEventListener('click', () => vscode.postMessage({ type: 'source', key: `${location.path}:${location.line}:${location.column}` }));
        return button;
    }
    function overrideLink(id, entries) {
        const winner = entries.get(id);
        const link = element('button', `⮧ ${winner.name}`, 'override-link');
        link.addEventListener('click', () => {
            const target = byId(id);
            target.scrollIntoView({ behavior: 'smooth', block: 'center' });
            target.focus();
        });
        return link;
    }
    function renderValue(value, entries) {
        const data = element('span', value.value, value.overriddenBy ? 'overridden' : '');
        const wrapper = element('span');
        wrapper.append(data);
        if (value.overriddenBy) { wrapper.append(' ', overrideLink(value.overriddenBy, entries)); }
        return wrapper;
    }
    function renderValues(values, entries) {
        const list = element('dl');
        const colors = new Set();
        for (const value of values) {
            const [property, channel] = value.property.split('.');
            if (['FontColor', 'BackgroundColor', 'BorderColor'].includes(property) && channel) {
                if (colors.has(property)) { continue; }
                colors.add(property);
                const channels = ['Alpha', 'Red', 'Green', 'Blue'].map(name => values.findLast(entry => entry.property === `${property}.${name}`));
                const row = element('dd', '', 'color-value');
                const cssHex = [1, 2, 3, 0].map(index => channels[index] ? Number(channels[index].value).toString(16).padStart(2, '0') : (index === 0 ? 'ff' : '00')).join('');
                const swatch = element('span', '', 'color-swatch');
                swatch.style.backgroundColor = `#${cssHex}`;
                swatch.title = `#${channels.map(entry => entry ? Number(entry.value).toString(16).padStart(2, '0') : '??').join('').toUpperCase()}`;
                row.append(swatch, '#');
                for (let index = 0; index < channels.length; index++) {
                    const entry = channels[index];
                    const part = element('span', entry ? Number(entry.value).toString(16).padStart(2, '0').toUpperCase() : '??', entry?.overriddenBy ? 'overridden' : '');
                    part.title = `${['Alpha', 'Red', 'Green', 'Blue'][index]}: ${entry.value}${entry.inherited ? ' (inherited)' : ''}`;
                    if (entry.inherited) { part.classList?.add('inherited'); }
                    row.append(part);
                }
                const winners = new Set(channels.map(entry => entry?.overriddenBy).filter(Boolean));
                for (const winner of winners) { row.append(' ', overrideLink(winner, entries)); }
                list.append(element('dt', property.replace(/([a-z])([A-Z])/g, '$1 $2')), row);
                continue;
            }
            const data = element('dd');
            data.append(renderValue(value, entries));
            list.append(element('dt', value.property.replaceAll('.', ' · '), value.overriddenBy ? 'overridden' : ''), data);
        }
        return list;
    }
    function defaultStyle(result) {
        const preview = result.defaultPreview;
        if (!preview) { return undefined; }
        const values = [];
        for (const [property, color] of [['FontColor', preview.foreground], ['BackgroundColor', preview.background], ['BorderColor', preview.border]]) {
            ['Red', 'Green', 'Blue', 'Alpha'].forEach((channel, index) => values.push({ property: `${property}.${channel}`, value: String(parseInt(color.slice(1 + index * 2, 3 + index * 2), 16)) }));
        }
        values.push({ property: 'FontSize', value: String(preview.fontSize) }, { property: 'Show', value: 'True' });
        for (const value of values) {
            value.overriddenBy = [...result.styles].reverse().find(style => style.values.some(entry => entry.property === value.property))?.id;
            if (!value.overriddenBy && value.property.includes('.')) {
                const property = value.property.split('.')[0];
                value.overriddenBy = result.styles.find(style => style.values.some(entry => entry.property.startsWith(`${property}.`)))?.id;
            }
        }
        return { id: 'default-style', name: 'Default style', values };
    }
    function styleColors(styles) {
        const inherited = new Map();
        return styles.map(style => {
            const values = [...style.values];
            for (const property of ['FontColor', 'BackgroundColor', 'BorderColor']) {
                if (!values.some(value => value.property.startsWith(`${property}.`))) { continue; }
                for (const channel of ['Alpha', 'Red', 'Green', 'Blue']) {
                    const key = `${property}.${channel}`;
                    const own = values.findLast(value => value.property === key);
                    if (own) { inherited.set(key, own.value); }
                    else { values.push({ property: key, value: inherited.get(key) ?? '0', inherited: true }); }
                }
            }
            return { ...style, values };
        });
    }
    function render(response) {
        latest = response;
        byId('status').textContent = '';
        byId('check').disabled = false;
        byId('errors').replaceChildren(...response.diagnostics.map(diagnostic => element('p', `${diagnostic.code}: ${diagnostic.message}`)));
        const results = byId('results');
        results.replaceChildren();
        byId('preview-section').hidden = !response.result?.preview;
        if (!response.result) { return; }
        const result = response.result;
        if (result.preview) {
            const preview = result.preview;
            renderIcon(result.effectiveStyle?.icon, preview.hidden);
            const label = byId('preview');
            scaleLabel(label, preview.fontSize);
            const baseType = result.item?.baseType;
            const namedEquipment = ['Rare', 'Unique'].includes(result.item?.rarity);
            label.textContent = namedEquipment && baseType && baseType !== preview.name
                ? `${preview.name}\n${baseType}` : preview.name;
            Object.assign(label.style, { color: gameColor(preview.foreground), backgroundColor: gameColor(preview.background),
                borderColor: result.effectiveStyle?.borderColor ? gameColor(preview.border) : preview.border, textDecoration: preview.hidden ? 'line-through' : 'none' });
        }
        results.append(element('h2', 'Applied styles'));
        const fallback = defaultStyle(result);
        const styles = styleColors(result.styles).reverse();
        if (fallback) { styles.push(fallback); }
        const entries = new Map(styles.map(style => [style.id, style]));
        for (const style of styles) {
            const article = element('article', '', 'style');
            article.id = style.id;
            article.tabIndex = -1;
            article.append(element('h3', style.name));
            if (style.location) { article.append(source(style.location)); }
            article.append(renderValues(style.values, entries));
            results.append(article);
        }
        results.append(element('h2', result.status === 'Matched' ? 'Block trace' : 'No matching block'));
        const frames = result.frames ?? [];
        const frameEntries = new Map(frames.map(frame => [frame.id, frame]));
        for (const frame of frames) {
            const article = element('article', '', 'style');
            article.id = frame.id;
            article.tabIndex = -1;
            article.append(source(frame.location));
            const declarations = element('dl');
            for (const value of frame.declarations) {
                const data = element('dd');
                const winner = value.name === 'Style' ? undefined : frameEntries.get(value.overriddenBy);
                data.append(element('span', value.value, winner ? 'overridden' : ''));
                if (winner) {
                    const link = source(winner.location);
                    link.textContent = `⮧ ${link.textContent}`;
                    data.append(' ', link);
                }
                declarations.append(element('dt', value.name, winner ? 'overridden' : ''), data);
            }
            article.append(declarations);
            results.append(article);
        }
    }
    window.addEventListener('message', event => {
        if (event.data.type === 'fields') { renderFields(event.data.fields); }
        if (event.data.type === 'result') { render(event.data.response); }
        if (event.data.type === 'input') { applyInput(event.data.input); if (latest) { render(latest); } }
        if (event.data.type === 'error') {
            byId('errors').textContent = event.data.message;
            byId('status').textContent = '';
            byId('check').disabled = false;
        }
    });
    if (currentInput) { applyInput(currentInput); }
    vscode.postMessage({ type: 'ready' });
})();
