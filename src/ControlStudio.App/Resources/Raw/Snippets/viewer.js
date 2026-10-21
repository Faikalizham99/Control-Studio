/* Saved code is data: never eval it, insert it as markup, or run it. */
(() => {
  const parsers = { JavaScript: 'babel', TypeScript: 'typescript', Json: 'json', Html: 'html', Css: 'css', Markdown: 'markdown' };
  const languages = { JavaScript: 'javascript', TypeScript: 'typescript', Json: 'json', Html: 'xml', Css: 'css', Markdown: 'markdown', CSharp: 'csharp', C: 'c', Cpp: 'cpp', Swift: 'swift', Python: 'python', Sql: 'sql', Shell: 'bash', Xml: 'xml' };
  const decode = value => new TextDecoder().decode(Uint8Array.from(atob(value), c => c.charCodeAt(0)));
  const encode = value => {
    const bytes = new TextEncoder().encode(value);
    let text = '';
    for (const byte of bytes) text += String.fromCharCode(byte);
    return btoa(text);
  };
  let code = '', language = 'PlainText', result = 'idle';
  const render = () => {
    const view = document.getElementById('code');
    const grammar = languages[language];
    view.textContent = code;
    // highlight.js escapes source characters. Only its generated spans become markup.
    if (grammar && hljs.getLanguage(grammar)) {
      try { view.innerHTML = hljs.highlight(code, { language: grammar, ignoreIllegals: true }).value; }
      catch { view.textContent = code; }
    }
    document.getElementById('lines').textContent = Array.from({ length: code.split('\n').length }, (_, i) => i + 1).join('\n');
  };
  window.studio = {
    set(value, selected, dark) {
      code = decode(value); language = selected; result = 'idle';
      this.theme(dark); render(); return 'ready';
    },
    theme(dark) {
      document.documentElement.dataset.theme = dark ? 'dark' : 'light';
      document.getElementById('light-style').disabled = !!dark;
      document.getElementById('dark-style').disabled = !dark;
    },
    async format() {
      if (result === 'pending') return;
      result = 'pending';
      try {
        const isClang = ['C', 'Cpp', 'CSharp'].includes(language);
        if (!isClang && !parsers[language]) throw new Error('unsupported');
        const formatted = isClang ? await window.studioClang.format(code, language) :
          await prettier.format(code, { parser: parsers[language], plugins: Object.values(prettierPlugins),
            tabWidth: 2, printWidth: 80, embeddedLanguageFormatting: 'off' });
        if (formatted.length > 50000) throw new Error('limit');
        code = formatted; render(); result = 'ok:' + encode(code);
      } catch (error) { result = error?.formatterUnavailable ? 'unavailable' : 'error'; }
    },
    result: () => result
  };
})();
