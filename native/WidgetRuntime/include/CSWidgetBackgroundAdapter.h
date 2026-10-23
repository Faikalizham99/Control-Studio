#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

/// Installs only in an iOS 27 widget extension. Unsupported runtimes are unchanged.
FOUNDATION_EXPORT void CSInstallWidgetBackgroundAdapter(void);

/// Exact allowlist: never changes Counter, controls, Live Activities or other kinds.
FOUNDATION_EXPORT BOOL CSIsStudioSlotKind(NSString * _Nullable kind);

/// Rewrites an independent secure-coded copy, or returns the original on failure.
/// The class parameter lets native tests exercise this without private frameworks.
FOUNDATION_EXPORT id _Nullable CSWidgetBackgroundRewriteResult(id _Nullable result,
    Class _Nullable descriptorClass) NS_SWIFT_NAME(CSWidgetBackgroundRewriteResult(_:descriptorClass:));

NS_ASSUME_NONNULL_END
