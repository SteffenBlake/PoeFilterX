const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { runInNewContext } = require('node:vm');
const path = require('node:path');

function inspector() {
    const nodes = new Map();
    class Element {
        constructor(tag) {
            this.tag = tag;
            this.children = [];
            this.style = {};
            this.dataset = {};
            this.events = {};
            this.value = '';
        }
        set id(value) { this._id = value; nodes.set(value, this); }
        get id() { return this._id; }
        set textContent(value) { this.text = value; this.children = []; }
        get textContent() { return (this.text ?? '') + this.children.map(child => typeof child === 'string' ? child : child.textContent).join(''); }
        get options() { return this.children; }
        get selectedOptions() { return this.options.filter(option => option.selected); }
        append(...children) { this.children.push(...children); }
        replaceChildren(...children) { this.text = ''; this.children = children; }
        addEventListener(name, callback) { this.events[name] = callback; }
        focus() { this.focused = true; }
        scrollIntoView() { }
    }
    for (const id of ['check-form', 'facts', 'item', 'game', 'area', 'clear', 'check', 'results', 'errors', 'preview-section', 'preview', 'status', 'preview-group', 'preview-icon']) {
        const node = new Element('div');
        node.id = id;
    }
    const games = ['Poe1', 'Poe2'].map(value => Object.assign(new Element('input'), { value, checked: value === 'Poe1' }));
    let receive;
    const messages = [];
    runInNewContext(readFileSync(path.join(__dirname, '../media/itemInspector.js'), 'utf8'), {
        document: {
            getElementById: id => nodes.get(id),
            createElement: tag => new Element(tag),
            querySelector: () => games.find(game => game.checked),
            querySelectorAll: () => games
        },
        window: {
            addEventListener: (_, callback) => { receive = callback; },
            getComputedStyle: () => ({ lineHeight: '49.5px', paddingTop: '2px', paddingBottom: '2px', borderTopWidth: '2px', borderBottomWidth: '2px' })
        },
        acquireVsCodeApi: () => ({ getState: () => undefined, setState() { }, postMessage: message => messages.push(message) })
    });
    return { nodes, messages, send: data => receive({ data }) };
}

const location = { path: '/project/entrypoint.filterx', line: 2, column: 1 };
const preview = { name: 'Chaos Orb', foreground: '#112233ff', background: '#000000ff', border: '#00000000', fontSize: 32, hidden: false, opacity: 1 };

test('styles descend in precedence, use ARGB, preserve channel overrides, and end with defaults', () => {
    const ui = inspector();
    ui.send({ type: 'result', response: { diagnostics: [], result: {
        status: 'Matched', preview: { ...preview, hidden: true, opacity: 1 }, defaultPreview: preview,
        styles: [
            { id: 'base', name: 'base', location, values: [
                { property: 'FontColor.Red', value: '17', overriddenBy: 'accent' },
                { property: 'FontColor.Green', value: '34' }, { property: 'FontColor.Blue', value: '51' },
                { property: 'FontColor.Alpha', value: '255' }
            ] },
            { id: 'accent', name: 'accent', location, values: [{ property: 'Show', value: 'False' }, { property: 'FontColor.Red', value: '99' }] }
        ], frames: []
    } } });
    const articles = ui.nodes.get('results').children.filter(node => node.tag === 'article');
    assert.deepEqual(articles.map(node => node.id), ['accent', 'base', 'default-style']);
    const color = ui.nodes.get('base').textContent;
    assert.match(color, /#FF112233/);
    assert.match(ui.nodes.get('accent').textContent, /#FF632233/);
    assert.doesNotMatch(ui.nodes.get('accent').textContent, /\?\?/);
    assert.match(color, /⮧ accent/);
    assert.doesNotMatch(color, /Overridden by|active/);
    assert.equal(ui.nodes.get('preview').style.opacity, undefined);
    assert.equal(ui.nodes.get('preview').style.textDecoration, 'line-through');
    assert.doesNotMatch(ui.nodes.get('results').textContent, /Hidden \(5%|In-game preview|Rule evaluation trace/);
});

test('trace renders matched ancestry in supplied bottom-up order and links replaced declarations', () => {
    const ui = inspector();
    ui.send({ type: 'result', response: { diagnostics: [], result: {
        status: 'Matched', preview, styles: [
            { id: 'base', name: 'base', location, values: [{ property: 'FontSize', value: '20', overriddenBy: 'accent' }] },
            { id: 'accent', name: 'accent', location, values: [{ property: 'FontSize', value: '45' }] }
        ], frames: [
            { id: 'child', location: { ...location, line: 112 }, declarations: [{ name: 'Corrupted', value: 'False' }, { name: 'Style', value: 'accent' }] },
            { id: 'parent', location: { ...location, line: 1 }, declarations: [{ name: 'Corrupted', value: 'True', overriddenBy: 'child' }, { name: 'Style', value: 'base' }] }
        ]
    } } });
    const articles = ui.nodes.get('results').children.filter(node => node.tag === 'article');
    assert.deepEqual(articles.map(node => node.id).slice(-2), ['child', 'parent']);
    const declarations = ui.nodes.get('parent').children.find(node => node.tag === 'dl');
    const scalar = declarations.children[1];
    assert.equal(scalar.children[0].className, 'overridden');
    const link = scalar.children.find(node => node.tag === 'button');
    assert.equal(link.textContent, '⮧ entrypoint.filterx:112');
    link.events.click();
    assert.equal(ui.messages.at(-1).type, 'source');
    assert.equal(ui.messages.at(-1).key, '/project/entrypoint.filterx:112:1');
    const style = declarations.children[3];
    assert.equal(style.textContent, 'base');
    assert.notEqual(style.children[0].className, 'overridden');
    assert.equal(style.children.length, 1);
});

test('typed facts round-trip and Clear Inputs clears item, overrides, and results', () => {
    const ui = inspector();
    ui.send({ type: 'fields', fields: [
        { name: 'Corrupted', kind: 'boolean', choices: [] },
        { name: 'ItemLevel', kind: 'number', choices: [] },
        { name: 'Class', kind: 'text', choices: [] },
        { name: 'HasInfluence', kind: 'text', choices: ['Shaper', 'Elder', 'None'] }
    ] });
    ui.send({ type: 'input', input: { item: 'copied', game: 'Poe2', areaLevel: 80, facts: { Corrupted: 'True', ItemLevel: '86', Class: 'Currency', HasInfluence: 'Shaper|Elder' } } });
    assert.equal(ui.nodes.get('fact-ItemLevel').type, 'number');
    assert.equal(ui.nodes.get('fact-Corrupted').tag, 'select');
    ui.nodes.get('check-form').events.submit({ preventDefault() { } });
    const submitted = JSON.parse(JSON.stringify(ui.messages.at(-1).input));
    assert.deepEqual(submitted.facts, { Corrupted: 'True', ItemLevel: '86', Class: 'Currency', HasInfluence: 'Shaper|Elder' });
    ui.nodes.get('clear').events.click();
    assert.equal(ui.nodes.get('item').value, '');
    assert.equal(ui.nodes.get('fact-ItemLevel').value, '');
    assert.equal(ui.nodes.get('fact-Corrupted').value, '');
    assert.equal(ui.nodes.get('preview-section').hidden, true);
    assert.equal(ui.messages.at(-1).type, 'clear');
});

for (const [name, baseType, rarity, expected] of [
    ["Orbala's Stand", 'Eelskin Boots', 'Unique', "Orbala's Stand\nEelskin Boots"],
    ['Doom Mantle', 'Vaal Regalia', 'Rare', 'Doom Mantle\nVaal Regalia'],
    ['Hale Eelskin Boots', 'Eelskin Boots', 'Magic', 'Hale Eelskin Boots'],
    ['Chaos Orb', 'Chaos Orb', 'Normal', 'Chaos Orb'],
    ['Chaos Orb', null, 'Normal', 'Chaos Orb']
]) {
    test(`item label displays ${JSON.stringify(expected)}`, () => {
        const ui = inspector();
        ui.send({ type: 'result', response: { diagnostics: [], result: {
            item: { baseType, rarity }, status: 'Matched', preview: { ...preview, name }, styles: [], frames: []
        } } });
        assert.equal(ui.nodes.get('preview').textContent, expected);
    });
}

test('label calibration and map icons retain their fixed sizes', () => {
    const ui = inspector();
    for (const fontSize of [1, 45]) {
        ui.send({ type: 'result', response: { diagnostics: [], result: {
            status: 'Matched', preview: { ...preview, fontSize }, styles: [], frames: [],
            effectiveStyle: { icon: { size: 1, color: 'Yellow', shape: 'Diamond' } }
        } } });
        assert.equal(ui.nodes.get('preview').style.fontSize, '45px');
        const rectangleHeight = 57.5 * Number(ui.nodes.get('preview').style.zoom);
        assert.ok(Math.abs(rectangleHeight - (fontSize === 1 ? 18.2 : 40.25)) < 0.001);
        assert.equal(ui.nodes.get('preview-group').style.zoom, undefined);
        assert.equal(ui.nodes.get('preview-icon').hidden, false);
        assert.equal(ui.nodes.get('preview-icon').src.split('/').pop(), 'LootFilterMediumYellowDiamond.png');
    }
    ui.send({ type: 'result', response: { diagnostics: [], result: {
        status: 'Matched', preview, styles: [], frames: [],
        effectiveStyle: { icon: { enabled: false, size: 1, color: 'Yellow', shape: 'Diamond' } }
    } } });
    assert.equal(ui.nodes.get('preview-icon').hidden, true);
});

test('every supported minimap icon has its game PNG artwork', () => {
    const enumValues = filename => readFileSync(path.join(__dirname, '../../../src/PoeFilterX.Engine/Compilation', filename), 'utf8')
        .match(/enum \w+\s*\{([\s\S]*?)\}/)[1].split(',').map(value => value.trim()).filter(Boolean);
    const shapes = enumValues('MiniMapIconShape.cs');
    const colors = enumValues('FilterColor.cs');
    for (const size of ['Large', 'Medium', 'Small']) {
        for (const color of colors) {
            for (const shape of shapes) {
                const filename = `LootFilter${size}${color}${shape}.png`;
                const bundled = readFileSync(path.join(__dirname, '../media/minimap-icons', filename));
                assert.equal(bundled.subarray(1, 4).toString(), 'PNG', filename);
                assert.equal(bundled.readUInt32BE(16), 64, filename);
                assert.equal(bundled.readUInt32BE(20), 64, filename);
            }
        }
    }
});

test('label proportions stay fixed while one-line rectangle heights increase across the filter range', () => {
    const ui = inspector();
    let previousHeight = 0;
    for (let fontSize = 1; fontSize <= 45; fontSize++) {
        ui.send({ type: 'result', response: { diagnostics: [], result: {
            status: 'Matched', preview: { ...preview, fontSize }, styles: [], frames: []
        } } });
        const label = ui.nodes.get('preview');
        assert.equal(label.style.fontSize, '45px');
        const height = 57.5 * Number(label.style.zoom);
        assert.ok(height > previousHeight, `size ${fontSize} must be taller than size ${fontSize - 1}`);
        previousHeight = height;
    }
    assert.equal(previousHeight, 40.25);
});

test('filter alpha maps to the calibrated opacity range and hidden items never preview icons', () => {
    const ui = inspector();
    const show = hidden => ui.send({ type: 'result', response: { diagnostics: [], result: {
        status: 'Matched', preview: { ...preview, hidden, foreground: '#11223300', background: '#44556680', border: '#778899ff' },
        styles: [], frames: [], effectiveStyle: { borderColor: { alpha: 255 }, icon: { size: 1, color: 'Yellow', shape: 'Diamond' } }
    } } });
    show(false);
    const label = ui.nodes.get('preview');
    assert.equal(label.style.color, 'rgba(17, 34, 51, 0.4)');
    assert.equal(label.style.backgroundColor, `rgba(68, 85, 102, ${0.4 + 0.6 * 128 / 255})`);
    assert.equal(label.style.borderColor, 'rgba(119, 136, 153, 1)');
    assert.equal(label.style.opacity, undefined);
    assert.equal(ui.nodes.get('preview-icon').hidden, false);
    show(true);
    assert.equal(ui.nodes.get('preview-icon').hidden, true);
    assert.equal(label.style.color, 'rgba(17, 34, 51, 0.4)');
    assert.equal(label.style.textDecoration, 'line-through');
    show(false);
    assert.equal(ui.nodes.get('preview-icon').hidden, false);
});
