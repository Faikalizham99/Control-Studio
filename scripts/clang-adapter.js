// Included in the generated private scope alongside the unmodified runtime/bindings.
let initialization;
window.studioClang = {
  async format(code, language) {
    const filename = { C: 'snippet.c', Cpp: 'snippet.cpp', CSharp: 'snippet.cs' }[language];
    if (!filename) throw new Error('unsupported');
    if (!initialization) {
      initialization = (async () => {
        const base64 = document.getElementById('clang-wasm').textContent.trim();
        if (!base64) throw new Error('missing formatter');
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const module = await WebAssembly.compile(bytes);
        set_wasm(createModule({ wasm: module }));
      })().catch(error => { initialization = undefined; throw error; });
    }
    try { await initialization; }
    catch {
      const error = new Error('formatter unavailable');
      error.formatterUnavailable = true;
      throw error;
    }
    return format(code, filename, JSON.stringify({
      BasedOnStyle: language === 'CSharp' ? 'Microsoft' : 'LLVM',
      IndentWidth: 4, ColumnLimit: 100, SortIncludes: 'Never',
      FixNamespaceComments: false, InsertBraces: false,
      RemoveBracesLLVM: false, RemoveParentheses: 'Leave',
      RemoveSemicolon: false, QualifierAlignment: 'Leave',
      ReflowComments: 'Never'
    }));
  }
};
