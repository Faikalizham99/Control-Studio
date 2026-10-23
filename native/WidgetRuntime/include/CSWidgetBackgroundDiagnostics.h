#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN
/// Bounded local snapshots. No network, user content or raw exception messages.
@interface CSWidgetBackgroundDiagnosticRecorder : NSObject
- (instancetype)initWithDirectory:(NSURL *)directory version:(NSString *)version
    build:(NSString *)build os:(NSString *)os;
- (void)recordStage:(NSString *)stage reason:(NSString *)reason detail:(NSString *)detail
    NS_SWIFT_NAME(record(stage:reason:detail:));
/// Intended for native tests; production records asynchronously off the callback.
- (void)flush;
@end

FOUNDATION_EXPORT void CSStartWidgetBackgroundDiagnostics(void);
FOUNDATION_EXPORT void CSRecordWidgetBackgroundEvent(NSString *stage, NSString *reason, NSString *detail);
NS_ASSUME_NONNULL_END
