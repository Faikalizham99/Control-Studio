# Bundled clock fonts

Unmodified regular TTFs from the official [Google Fonts repository](https://github.com/google/fonts/tree/e7db6ca603fd4862010383ff17ffd6cc4ffac1c0/ofl), revision `e7db6ca603fd4862010383ff17ffd6cc4ffac1c0`:

- [RocknRoll One](https://github.com/google/fonts/tree/e7db6ca603fd4862010383ff17ffd6cc4ffac1c0/ofl/rocknrollone)
- [Dela Gothic One](https://github.com/google/fonts/tree/e7db6ca603fd4862010383ff17ffd6cc4ffac1c0/ofl/delagothicone)
- [Kaisei Decol](https://github.com/google/fonts/tree/e7db6ca603fd4862010383ff17ffd6cc4ffac1c0/ofl/kaiseidecol)

Each has its original SIL Open Font License alongside it. Font binaries and licence texts are copied into the host app and WidgetKit extension; no runtime font downloads or photo permissions are needed. The three faces total 9,694,824 bytes before package compression, and are included in both bundles. They are complete fonts, not subsets or modified versions. The manifest records source revision, filenames, PostScript names and SHA-256 digests.

`UIAppFonts` registers these names at bundle launch. MAUI labels, UIKit export and SwiftUI live clocks refer to the same PostScript names. Do not replace a file with a differently named font without updating both platform mappings and the manifest. Packaging checks verify font bytes, glyph coverage, registration and resource build phases.
