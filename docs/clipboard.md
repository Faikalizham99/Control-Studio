# Clipboard

The owner authorized functional Text / Photo / Link / Snippet sections on 21 October 2026. This extends the Studio prototype. The top switcher uses local vector icons, accent-aware selection and native buttons; Add creates the currently selected type. Each section retains saved order, Edit and confirmed Delete. There is no automatic clipboard reading or background history.

| Section | Actions and storage |
|---|---|
| Text | Copy; exact whitespace and Unicode, up to 10,000 characters. |
| Link | Copy or Open in the default browser; validated HTTP(S) only, up to 2,048 characters. Former saved web-link actions now appear here. |
| Photo | Import one image from Photo Library or Files, Copy image, Preview, Share / Save to Photos. Private PNG copy, orientation corrected, longest side at most 4,096 pixels, with a 512-pixel thumbnail. Source at most 10 MB, saved PNG at most 20 MB. Transparency is retained where supported; animation/Live Photo behavior and original metadata are not retained. |
| Snippet | Save and edit code up to 50,000 characters with an explicit language, View code, Copy, Beautify, Original and Save formatting. The editor's Preview / Beautify only changes the draft when Use formatting is chosen; the editor still needs Save. |

All names remain limited to 60 characters. Existing record IDs, metadata and ordering survive edits. Text and code are never trimmed; only outer whitespace in names/URLs is removed. Generic counter records remain preserved and hidden. User content is never included in application logs.

## Photos

Photo Library uses the existing MAUI `PickPhotosAsync` / iOS PHPicker path, with one selection and no broad library access. Files remains available. Originals in Photos/Files are never edited or deleted. Cancelled/replaced drafts and successfully replaced/deleted saved images are cleaned up. A database mutation finishes before its referenced file is removed; navigation cancellation cannot delete an image that was just committed. Process termination can leave an unreferenced private draft file; there is no automatic storage sweep yet.

Copy writes an actual `UIImage` to `UIPasteboard`, not a file path or Base64 string. The destination app must accept image pastes. Preview displays the saved copy. The native share sheet offers sharing and, where available, Save Image. `NSPhotoLibraryAddUsageDescription` explains that explicit save operation, as required by [MAUI media sharing](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/share?view=net-maui-10.0). No undocumented Photos URL scheme is used to open a particular asset. The share sheet creates a separate copy in Photos; opening Photos and locating that copy is handled by iOS/the user.

## Snippets

Bundled [Prettier standalone](https://prettier.io/docs/browser) **3.9.10** formats JavaScript/JSX, TypeScript/TSX, JSON, HTML, CSS and Markdown offline. Bundled [clang-format WebAssembly](https://github.com/wasm-fmt/clang-format) **23.1.1** adds offline Beautify for **C, C++ and C#**. These three have dedicated language selections and syntax colouring. New enum members are appended so existing persisted language IDs stay unchanged. Formatting is not compilation or a complete syntax check; clang-format can format partial code.

The C/C++ style uses LLVM and C# uses Microsoft, with four-space indentation and a 100-column limit. Include sorting, comment reflow, namespace-comment insertion, and optional brace/parenthesis/semicolon/qualifier rewrites are disabled. The formatter bytes are embedded as inert Base64 only for C/C++/C# previews and compiled lazily on the first Beautify; neither the module nor any code is fetched from the network. The WebView CSP permits WebAssembly compilation with `wasm-unsafe-eval` while JavaScript eval and network access stay disabled. Initialization failures preserve code and show a retry message.

Formatting failures or output above 50,000 characters leave the current code intact. Original restores the opening code in the preview; Save formatting is explicit and persists only a changed preview. Swift, Python, SQL, Shell and XML have syntax colouring but no bundled formatter; the UI explains this and disables Beautify. Plain text is supported too.

[highlight.js](https://highlightjs.org/) **11.11.1** provides explicit-language syntax colouring, with a bundled Swift grammar. Line numbers and horizontal scrolling keep code readable. Light/Dark mode follows the app. Code remains selectable and copyable. The native editor uses Menlo with autocorrection/spellcheck disabled for snippets.

Saved code is passed to a fixed viewer function as UTF-8 Base64, displayed through text/escaped highlighter output, and never executed. The HTML is built only from trusted packaged assets. A restrictive content security policy prevents network requests, images and frames, and navigation is limited to the initial app-bundle document. MAUI's pinned [iOS WebView implementation](https://github.com/dotnet/maui/blob/10.0.110/src/Core/src/Platform/iOS/MauiWKWebView.cs) uses the bundle directory for its initial simulated HTML request; the navigation guard explicitly permits this initial URL and blocks later navigation. No snippet is transmitted, rendered as live HTML, or used as a script/plugin.

Vendor code is checked in under `Resources/Raw/Snippets/vendor`, with licenses (including LLVM's Apache license/exception notice) and a manifest containing pinned package URLs, npm SHA-512 integrity values and each file's SHA-256. There are no runtime CDN requests or new NuGet dependencies. Formatter/highlighter assets add about 5 MB before IPA compression, including the 2.57 MB clang-format WASM module. `python scripts/bundle-clang.py` reproducibly wraps the original clang runtime/binding exports in a private classic script with the adapter in `scripts/clang-adapter.js`; packaging tests verify that generated output stays in sync. Original vendor files remain byte-identical to the pinned package.

## Persistence and verification

The private SQLite action document now supports typed `photo` (GUID asset reference only) and `snippet` parameters in addition to existing records. Readers accept database/document versions 1 and 2. Reads preserve old data without rewriting it. A successful mutation upgrades the private database/document to version 2 atomically. Earlier releases reject version 2 instead of overwriting it, so downgrading will not open this clipboard database. A 16-million-character serialized document bound is enforced before writes as well as reads. The App Group counter schema, native widgets, App Intents and signing entitlements are unchanged.

Local checks passed on 21 October 2026: **138 automated tests** (42 Core, 19 infrastructure, 37 app, 23 packaging and 17 offline snippet tests), managed iOS C# compilation on Windows, source/plist/App Group checks, workflow lint and whitespace checks. C-family tests cover formatting stability, literal/comment/include-order preservation, unavailable-module retry, persisted languages and stable old language IDs. Native iOS linking and device execution were not run. Commands:

```powershell
dotnet test tests/ControlStudio.Core.Tests -c Release --no-restore
dotnet test tests/ControlStudio.Infrastructure.Tests -c Release --no-restore
dotnet test tests/ControlStudio.App.Tests -c Release --no-restore
node --test tests/snippets/viewer.test.cjs
python -m unittest discover -s tests/packaging
python scripts/check-source.py
python scripts/bundle-clang.py --check
```

Phone checks remain **NOT VERIFIED**: switcher layout, Light/Dark/accents and VoiceOver, all four editors and keyboard behavior, old data after update/relaunch, PHPicker/Files cancellation, oriented/transparent/large/missing images, actual image pasting and Photos share-sheet saving (including permission refusal), WKWebView loading/CSP, syntax colours, horizontal scrolling, format/reset/save/cancel, fast navigation and native app linking. Node tests execute the shipped scripts with a minimal DOM; they do not substitute for WKWebView/device rendering.

The owner authorized committing and pushing the Clipboard implementation and C/C++/C# support on 21 October 2026. The owner subsequently authorized tag **v0.2.5**, version **0.2.5 (8)**, with the button-label contrast refinement. The owner monitors the tag build personally; native CI, final IPA and phone results remain unverified here.

GitHub API inspection subsequently confirmed that v0.2.5 exists and the IPA workflow is enabled, but no IPA run was created for that tag; the cause was not exposed by the API. The owner authorized a fresh **v0.2.6**, version **0.2.6 (9)**, commit and tag push as a retry. This changes release metadata only and preserves v0.2.5. Verification is limited to whether the new run is created; the owner monitors its progress and outcome.
