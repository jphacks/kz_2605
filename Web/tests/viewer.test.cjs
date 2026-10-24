// Offline interaction checks: no Firebase connection or production writes.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

class Element {
  constructor() {
    this.children = []; this.events = {}; this.style = {}; this.dataset = {};
    this.value = ''; this.hidden = false; this.className = ''; this.attributes = {};
    this.classList = {
      toggle: (name, on) => {
        const classes = new Set(this.className.split(' ').filter(Boolean));
        if (on ?? !classes.has(name)) classes.add(name); else classes.delete(name);
        this.className = [...classes].join(' ');
      },
      add: name => this.classList.toggle(name, true), remove: name => this.classList.toggle(name, false),
      contains: name => this.className.split(' ').includes(name),
    };
  }
  set innerHTML(value) {
    this.parts = {};
    for (const match of value.matchAll(/class="([^"]+)"/g))
      for (const name of match[1].split(' ')) this.parts['.' + name] = new Element();
  }
  addEventListener(event, handler) { (this.events[event] ||= []).push(handler); }
  async fire(event) {
    for (const handler of this.events[event] || []) await handler({ preventDefault() {}, currentTarget: this });
  }
  setAttribute(name, value) { this.attributes[name] = value; }
  replaceChildren(...children) { this.children = children; }
  append(...children) { this.children.push(...children); }
  prepend(child) { this.children.unshift(child); }
  querySelector(selector) { return this.parts?.[selector] || this.children.find(c => c.tag === selector) || null; }
  get firstElementChild() { return this.children[0]; }
  scrollIntoView() { throw new Error('Voting must not scroll the page'); }
}

function fixture() {
  const elements = new Map();
  const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
  get('choice-timebar').append(new Element());
  const ratings = ['good', 'bad'].map(rating => { const e = new Element(); e.dataset.rating = rating; return e; });
  const submit = new Element(); submit.tag = 'button'; get('feedback-form').append(submit);
  const writes = [];
  const document = {
    body: new Element(), getElementById: get,
    createElement: tag => { const e = new Element(); e.tag = tag; return e; },
    querySelectorAll: selector => selector === '[data-rating]' ? ratings : [],
  };
  const context = vm.createContext({
    document, window: {}, location: { search: '', href: 'http://localhost/' }, URLSearchParams,
    localStorage: { getItem() { return null; }, setItem() {} },
    initializeApp: () => ({}), getDatabase: () => ({}), ref: (_, p) => p,
    onValue() {}, onChildAdded() {}, off() {}, query: p => p, limitToLast() {},
    set: async (p, v) => writes.push({ path: p, value: v }),
    push: async (p, v) => writes.push({ path: p, value: v }),
    serverTimestamp: () => 12345, onDisconnect: () => ({ remove: async () => {} }),
    setInterval() {}, DATABASE_URL: '', DEFAULT_ROOM: 'test', console,
  });
  const source = fs.readFileSync(path.join(__dirname, '../public/app.js'), 'utf8')
    .replace(/^import[\s\S]*?from\s+"[^"]+";\s*/gm, '');
  vm.runInContext(source + '\nglobalThis.api={data,renderChoice,castVote,renderEnding,updateLive};', context);
  return { get, ratings, writes, api: context.api, document };
}

test('Voting remains visible without scrolling, and timer updates keep the same buttons', async () => {
  const f = fixture();
  f.api.data.choice = { id: 'one', status: 'open', question: 'どうする？', remaining: 10, timeLimit: 10,
    options: [{ label: '行く' }, { label: '行かない' }] };
  f.api.renderChoice();
  assert.equal(f.get('choice').hidden, false);
  assert.equal(f.get('choice').classList.contains('voting-open'), true);
  const first = f.get('choice-options').children[0];
  f.api.data.choice.remaining = 9;
  f.api.renderChoice();
  assert.equal(f.get('choice-options').children[0], first);
  await first.fire('click');
  assert.equal(f.writes[0].value, 0);
  f.api.data.choice.status = 'closed'; f.api.data.choice.result = 0;
  f.api.renderChoice();
  assert.equal(first.disabled, true);
  assert.equal(f.document.body.classList.contains('voting-open'), false);
  f.api.data.choice = null; f.api.renderChoice();
  assert.equal(f.get('choice').hidden, false);
  assert.equal(f.get('choice-options').children.length, 0);
});

test('Ending feedback sends positive/negative ratings and comments after the stream ends', async () => {
  const f = fixture();
  f.api.data.state = { phase: 'ended' };
  f.api.data.ending = { id: 'ED9', title: '竜宮城からの脱出', genre: '脱出' };
  f.api.renderEnding();
  assert.equal(f.get('feedback').hidden, false);
  await f.ratings[0].fire('click'); await f.ratings[1].fire('click');
  assert.deepEqual(f.writes.map(w => w.value.feedback), ['good', 'bad']);
  assert.equal(f.ratings[1].attributes['aria-pressed'], 'true');
  f.get('feedback-text').value = '逃げるシーンがよかった';
  await f.get('feedback-form').fire('submit');
  assert.equal(f.writes[2].value.ending, 'ED9');
  assert.equal(f.writes[2].value.text, '逃げるシーンがよかった');
  assert.equal(f.get('feedback-text').value, '');
  assert.match(f.get('feedback-status').textContent, /送りました/);
});

test('Mobile interaction CSS keeps pinch zoom while preventing button double-tap zoom and input auto-zoom', () => {
  const css = fs.readFileSync(path.join(__dirname, '../public/style.css'), 'utf8');
  const html = fs.readFileSync(path.join(__dirname, '../public/index.html'), 'utf8');
  assert.match(css, /\.reaction\s*\{[^}]*touch-action:\s*manipulation/);
  assert.match(css, /\.comment-form input\s*\{[^}]*font-size:\s*16px/);
  assert.doesNotMatch(html, /user-scalable=no|maximum-scale=1/);
});
