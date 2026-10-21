const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../../src/ControlStudio.App/Resources/Raw/Snippets');
const scripts = ['vendor/prettier/standalone.js', 'vendor/prettier/plugins/babel.js', 'vendor/prettier/plugins/estree.js',
  'vendor/prettier/plugins/typescript.js', 'vendor/prettier/plugins/html.js', 'vendor/prettier/plugins/postcss.js',
  'vendor/prettier/plugins/markdown.js', 'vendor/highlight/highlight.min.js', 'vendor/highlight/languages/swift.min.js', 'clang-formatter.js', 'viewer.js'];
const clangWasm = fs.readFileSync(path.join(root, 'vendor/clang/clang-format.wasm')).toString('base64');
const encode = text => Buffer.from(text).toString('base64');
function viewer() {
  const elements = { 'clang-wasm': { textContent: clangWasm } };
  const context = { TextDecoder, TextEncoder, atob, btoa, console, setTimeout, clearTimeout,
    document: { documentElement: { dataset: {} }, getElementById: id => elements[id] ??= { textContent: '', innerHTML: '', disabled: false } } };
  context.window = context;
  context.fetch = () => { throw new Error('Network requests are forbidden'); };
  vm.createContext(context, { codeGeneration: { strings: false, wasm: true } });
  for (const script of scripts) vm.runInContext(fs.readFileSync(path.join(root, script), 'utf8'), context, { filename: script, timeout: 5000 });
  return { studio: context.studio, context, elements };
}

test('vendor assets match their pinned checksums and include their licenses', () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(root, 'vendor/manifest.json'), 'utf8'));
  for (const [folder, pkg] of Object.entries(manifest)) {
    assert.ok(pkg.files.LICENSE);
    for (const [file, checksum] of Object.entries(pkg.files))
      assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(root, 'vendor', folder, file))).digest('hex'), checksum);
  }
  for (const file of scripts) assert.ok(!fs.readFileSync(path.join(root, file), 'utf8').toLowerCase().includes('</script'));
});

for (const [language, source, expected] of [
  ['JavaScript', "const cat={name:'🐱',count:2};", 'const cat ='],
  ['TypeScript', 'const value:{count:number}={count:1};', 'count: number'],
  ['Json', '{"name":"🐱","array":[1,2,3]}', '"name": "🐱"'],
  ['Html', '<div><span>Hello</span><span>World</span></div>', '<div>'],
  ['Css', 'body{color:red;background:black}', 'color: red;'],
  ['Markdown', '# Title\n\n-   first\n-   second', '- first'],
  ['C', '#include <stdio.h>\nint main(){int n=1;if(n){printf("hello\\n");}return 0;}', 'int n = 1;'],
  ['Cpp', '#include <vector>\nint main(){std::vector<int> values={1,2};for(auto n:values){if(n>0){continue;}}return 0;}', 'std::vector<int> values ='],
  ['CSharp', 'class Example{public int Add(int a,int b){return a+b;}}', 'return a + b;']
]) test(`Beautify formats ${language} offline and a second pass is stable`, async () => {
  const { studio } = viewer(); studio.set(encode(source), language, false);
  await studio.format(); assert.ok(studio.result().startsWith('ok:'));
  const formatted = Buffer.from(studio.result().slice(3), 'base64').toString('utf8');
  assert.ok(formatted.includes(expected)); assert.notEqual(formatted, source);
  await studio.format(); assert.equal(Buffer.from(studio.result().slice(3), 'base64').toString('utf8'), formatted);
});

test('invalid syntax and unsupported languages preserve original code', async () => {
  for (const [language, source] of [['Json', '{broken'], ['Python', 'print("🐱")']]) {
    const { studio, elements } = viewer(); studio.set(encode(source), language, false);
    await studio.format(); assert.equal(studio.result(), 'error'); assert.equal(elements.code.textContent, source);
  }
});

test('source HTML and JavaScript are displayed as data and never executed', async () => {
  const { studio, context, elements } = viewer();
  const source = '</code><img src="https://evil.test" onerror="window.pwned=1"><script>window.pwned=2</script>🐱';
  studio.set(encode(source), 'Html', true);
  assert.equal(context.pwned, undefined); assert.equal(elements.code.textContent, source);
  assert.ok(!elements.code.innerHTML.includes('<script>')); assert.ok(!elements.code.innerHTML.includes('<img '));
  assert.ok(elements.code.innerHTML.includes('&lt;'));
  assert.equal(context.document.documentElement.dataset.theme, 'dark');
  studio.theme(false); assert.equal(elements['dark-style'].disabled, true);
});

test('code preview has line numbers and common language grammars', () => {
  const { studio, context, elements } = viewer(); studio.set(encode('line one\nline two\n'), 'PlainText', false);
  assert.equal(elements.lines.textContent, '1\n2\n3');
  for (const name of ['javascript', 'typescript', 'json', 'xml', 'css', 'markdown', 'csharp', 'c', 'cpp', 'swift', 'python', 'sql', 'bash'])
    assert.ok(context.hljs.getLanguage(name), `Missing grammar: ${name}`);
});

test('C-family formatting preserves preprocessor order, literals and comments', async () => {
  for (const [language, source, preserved] of [
    ['C', '#include "z.h"\n#include "a.h"\n#define APPLY(x) do { x; } while(0)\nint f(){const char* s="{; // 🐱}";/* keep { ; } */return 1;}', ['#include "z.h"\n#include "a.h"', '"{; // 🐱}"', '/* keep { ; } */']],
    ['Cpp', 'auto s=R"tag({ // ; 🐱 })tag";\ntemplate<class T> T f(T v){return v;}\n', ['R"tag({ // ; 🐱 })tag"']],
    ['CSharp', 'class C{string s=@"{ ; ""cat"" }";string p=$"value: {1+2}";}', ['@"{ ; ""cat"" }"']]
  ]) {
    const { studio } = viewer(); studio.set(encode(source), language, false); await studio.format();
    assert.ok(studio.result().startsWith('ok:'), `Failed ${language}`);
    const formatted = Buffer.from(studio.result().slice(3), 'base64').toString('utf8');
    for (const literal of preserved) assert.ok(formatted.includes(literal), `Changed literal in ${language}`);
  }
});

test('unavailable WASM preserves code and allows retry after restoring the bundled bytes', async () => {
  const { studio, elements } = viewer(); const source = 'int main(){return 0;}';
  elements['clang-wasm'].textContent = ''; studio.set(encode(source), 'C', false);
  await studio.format(); assert.equal(studio.result(), 'unavailable'); assert.equal(elements.code.textContent, source);
  elements['clang-wasm'].textContent = clangWasm;
  await studio.format(); assert.ok(studio.result().startsWith('ok:'));
});

test('CSP permits bundled WebAssembly without enabling JavaScript eval or network access', () => {
  const html = fs.readFileSync(path.join(root, 'viewer.html'), 'utf8');
  assert.ok(html.includes("'wasm-unsafe-eval'")); assert.ok(!html.includes("'unsafe-eval'"));
  assert.ok(html.includes("connect-src 'none'"));
});

test('50,000-character source stays bounded after formatting and failure keeps the source', async () => {
  const { studio, elements } = viewer(); const source = JSON.stringify(Array.from({length: 5000}, () => ({a: 1})));
  assert.ok(source.length <= 50000);
  studio.set(encode(source), 'Json', false); await studio.format();
  assert.equal(studio.result().slice(0, 5), 'error'); assert.equal(elements.code.textContent, source);
});
