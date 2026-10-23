#import "include/CSWidgetBackgroundAdapter.h"
#import "include/CSWidgetBackgroundDiagnostics.h"
#import <TargetConditionals.h>
#import <dispatch/dispatch.h>
#import <objc/runtime.h>
#import <os/lock.h>
#import <stdatomic.h>
#import <stdlib.h>
#import <string.h>

// An independent adapter for the descriptor mechanism demonstrated by
// https://github.com/pookjw/ClearAndBlurredWidgets (FooObject.mm).
// Use public secure-unarchiver substitution to preserve the complete result;
// do not rebuild its private Swift class through a raw initWithCoder: message.

@protocol CSDescriptorBackgroundMethods <NSObject>
- (NSString *)kind;
- (void)setBackgroundRemovable:(BOOL)value;
- (void)setTransparent:(BOOL)value;
// iOS 27 device diagnostics report the signed integer encoding q, not Q.
- (void)setPreferredBackgroundStyle:(NSInteger)value;
@end

static NSString *CSSafeErrorCode(NSError *error) {
    if (!error) return @"";
    NSString *domain = [@[NSCocoaErrorDomain, NSPOSIXErrorDomain, NSOSStatusErrorDomain] containsObject:error.domain] ? error.domain : @"other";
    return [NSString stringWithFormat:@"domain=%@ code=%ld", domain, (long)error.code];
}

BOOL CSIsStudioSlotKind(NSString *kind) {
    if (![kind isKindOfClass:NSString.class]) return NO;
    static NSSet<NSString *> *kinds;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        kinds = [NSSet setWithArray:@[@"ControlStudio.Slot.1", @"ControlStudio.Slot.2",
            @"ControlStudio.Slot.3", @"ControlStudio.Slot.4", @"ControlStudio.Slot.5",
            @"ControlStudio.Slot.6", @"ControlStudio.Slot.7", @"ControlStudio.Slot.8",
            @"ControlStudio.Slot.9", @"ControlStudio.Slot.10"]];
    });
    return [kinds containsObject:kind];
}

static BOOL CSMethodMatches(id object, SEL selector, const char *returnType,
    const char *argumentType) {
    if (![object respondsToSelector:selector]) return NO;
    Method method = class_getInstanceMethod([object class], selector);
    if (!method || method_getNumberOfArguments(method) != (argumentType ? 3 : 2)) return NO;
    char *returns = method_copyReturnType(method);
    BOOL matches = returns && strcmp(returns, returnType) == 0;
    if (!matches) CSRecordWidgetBackgroundEvent(@"abi", @"return-type-mismatch",
        [NSString stringWithFormat:@"%@ expected=%s actual=%s", NSStringFromSelector(selector), returnType, returns ?: "missing"]);
    free(returns);
    if (argumentType) {
        char *argument = method_copyArgumentType(method, 2);
        // Apple BOOL is encoded as B or c depending on architecture. Both
        // occupy one byte; accept either for the two Boolean setters only.
        BOOL boolSetter = selector == @selector(setBackgroundRemovable:)
            || selector == @selector(setTransparent:);
        BOOL compatible = argument && (strcmp(argument, argumentType) == 0
            || (boolSetter && (strcmp(argument, "B") == 0 || strcmp(argument, "c") == 0)));
        if (!compatible) CSRecordWidgetBackgroundEvent(@"abi", @"argument-type-mismatch",
            [NSString stringWithFormat:@"%@ expected=%s actual=%s", NSStringFromSelector(selector), argumentType, argument ?: "missing"]);
        matches = matches && compatible;
        free(argument);
    }
    return matches;
}

@interface CSBackgroundDecodeDelegate : NSObject <NSKeyedUnarchiverDelegate>
@property(nonatomic) Class descriptorClass;
@property(nonatomic) BOOL failed;
@property(nonatomic) NSUInteger patchedCount;
@property(nonatomic, strong) NSString *failureReason;
@property(nonatomic, strong) NSMutableArray<NSString *> *patchedKinds;
@end

@implementation CSBackgroundDecodeDelegate
- (id)unarchiver:(NSKeyedUnarchiver *)unarchiver didDecodeObject:(id)object {
    if (![object isKindOfClass:self.descriptorClass]) return object;
    if (!CSMethodMatches(object, @selector(kind), @encode(id), NULL)) {
        self.failed = YES; self.failureReason = @"kind-getter-unavailable-or-incompatible"; return object;
    }
    id<CSDescriptorBackgroundMethods> descriptor = object;
    NSString *kind = descriptor.kind;
    if (!CSIsStudioSlotKind(kind)) return object;
    if (![object conformsToProtocol:@protocol(NSMutableCopying)]) {
        self.failed = YES; self.failureReason = @"mutable-copy-protocol-unavailable"; return object;
    }
    id<CSDescriptorBackgroundMethods> copy = [object mutableCopy];
    if (!copy || copy == object || ![(id)copy isKindOfClass:self.descriptorClass]) {
        self.failed = YES; self.failureReason = @"mutable-copy-invalid"; return object;
    }
    for (NSString *name in @[@"setBackgroundRemovable:", @"setTransparent:", @"setPreferredBackgroundStyle:"]) {
        SEL setter = NSSelectorFromString(name);
        const char *type = setter == @selector(setPreferredBackgroundStyle:) ? @encode(NSInteger) : @encode(BOOL);
        if (!CSMethodMatches(copy, setter, @encode(void), type)) {
            self.failed = YES; self.failureReason = [@"setter-unavailable-or-incompatible:" stringByAppendingString:name]; return object;
        }
    }
    [copy setBackgroundRemovable:YES];
    [copy setTransparent:YES];
    [copy setPreferredBackgroundStyle:1]; // Clear request; system chrome is device-verified separately.
    self.patchedCount++;
    [self.patchedKinds addObject:kind];
    return copy;
}
@end

id CSWidgetBackgroundRewriteResult(id result, Class descriptorClass) {
    if (!result || !descriptorClass || ![result conformsToProtocol:@protocol(NSSecureCoding)]) {
        CSRecordWidgetBackgroundEvent(@"rewrite", !result ? @"nil-result" : (!descriptorClass ? @"descriptor-class-missing" : @"result-not-secure-codable"), @"");
        return result;
    }
    @try {
        NSError *error = nil;
        NSData *data = [NSKeyedArchiver archivedDataWithRootObject:result requiringSecureCoding:YES error:&error];
        if (error || !data || data.length > 8 * 1024 * 1024) {
            CSRecordWidgetBackgroundEvent(@"rewrite", data.length > 8 * 1024 * 1024 ? @"archive-too-large" : @"archive-failed", CSSafeErrorCode(error)); return result;
        }
        NSKeyedUnarchiver *decoder = [[NSKeyedUnarchiver alloc] initForReadingFromData:data error:&error];
        if (!decoder || error) { CSRecordWidgetBackgroundEvent(@"rewrite", @"decoder-create-failed", CSSafeErrorCode(error)); return result; }
        decoder.requiresSecureCoding = YES;
        decoder.decodingFailurePolicy = NSDecodingFailurePolicySetErrorAndReturn;
        CSBackgroundDecodeDelegate *rewrite = [CSBackgroundDecodeDelegate new];
        rewrite.descriptorClass = descriptorClass;
        rewrite.patchedKinds = [NSMutableArray new];
        decoder.delegate = rewrite;
        id updated = [decoder decodeObjectOfClass:[result class] forKey:NSKeyedArchiveRootObjectKey];
        [decoder finishDecoding];
        if (!updated || decoder.error || rewrite.failed || !rewrite.patchedCount || ![updated isKindOfClass:[result class]]) {
            NSString *reason = rewrite.failed ? rewrite.failureReason : ((!updated || decoder.error) ? @"secure-decode-failed" : (!rewrite.patchedCount ? @"no-matching-slot-descriptors" : @"unexpected-result-class"));
            CSRecordWidgetBackgroundEvent(@"rewrite", reason ?: @"unknown-failure", CSSafeErrorCode(decoder.error)); return result;
        }
        CSRecordWidgetBackgroundEvent(@"rewrite", @"changed-result-returned",
            [NSString stringWithFormat:@"count=%lu kinds=%@", (unsigned long)rewrite.patchedCount, [rewrite.patchedKinds componentsJoinedByString:@","]]);
        NSLog(@"[ControlStudioWidgetBackground] clear-descriptors-applied count=%lu", (unsigned long)rewrite.patchedCount);
        return updated;
    } @catch (NSException *exception) {
        (void)exception;
        // Never log exception reasons: private archives may contain personal data.
        NSLog(@"[ControlStudioWidgetBackground] descriptor-rewrite-fallback");
        CSRecordWidgetBackgroundEvent(@"rewrite", @"exception-caught", @"");
        return result;
    }
}

#if TARGET_OS_IOS
static _Atomic(IMP) CSOriginalFetch;
static os_unfair_lock CSInstallLock = OS_UNFAIR_LOCK_INIT;

static void CSFetchDescriptors(id object, SEL selector, void (^completion)(id)) {
    IMP implementation = atomic_load_explicit(&CSOriginalFetch, memory_order_acquire);
    if (!implementation) return; // Installation publishes the original before replacing the method.
    void (*original)(id, SEL, void (^)(id)) = (void (*)(id, SEL, void (^)(id)))implementation;
    if (!completion) { original(object, selector, nil); return; }
    CSRecordWidgetBackgroundEvent(@"fetch", @"requested", @"");
    original(object, selector, ^(id result) {
        @autoreleasepool {
            CSRecordWidgetBackgroundEvent(@"callback", @"received", @"");
            id updated = CSWidgetBackgroundRewriteResult(result, objc_lookUpClass("CHSWidgetDescriptor"));
            completion(updated);
        }
    });
}

static void CSInstallIfReady(void) {
    os_unfair_lock_lock(&CSInstallLock);
    @try {
        if (atomic_load_explicit(&CSOriginalFetch, memory_order_acquire)) return;
        Class exported = objc_lookUpClass("_TtCC9WidgetKit24WidgetExtensionXPCServer14ExportedObject");
        Class descriptor = objc_lookUpClass("CHSWidgetDescriptor");
        SEL selector = sel_registerName("getAllCurrentDescriptorsWithCompletion:");
        Method method = exported ? class_getInstanceMethod(exported, selector) : NULL;
        if (!exported || !descriptor || !method || method_getNumberOfArguments(method) != 3) {
            CSRecordWidgetBackgroundEvent(@"install", !exported ? @"exported-object-class-missing" : (!descriptor ? @"descriptor-class-missing" : (!method ? @"fetch-method-missing" : @"fetch-argument-count-mismatch")), @""); return;
        }
        char *returns = method_copyReturnType(method);
        char *argument = method_copyArgumentType(method, 2);
        BOOL compatible = returns && argument && strcmp(returns, @encode(void)) == 0 && strcmp(argument, "@?") == 0;
        NSString *signature = compatible ? @"" : [NSString stringWithFormat:@"return=%s completion=%s", returns ?: "missing", argument ?: "missing"];
        free(returns); free(argument);
        if (!compatible) {
            CSRecordWidgetBackgroundEvent(@"install", @"fetch-signature-mismatch", signature); return;
        }
        IMP original = method_getImplementation(method);
        if (!original || original == (IMP)CSFetchDescriptors) {
            CSRecordWidgetBackgroundEvent(@"install", @"original-implementation-unavailable", @""); return;
        }
        atomic_store_explicit(&CSOriginalFetch, original, memory_order_release);
        method_setImplementation(method, (IMP)CSFetchDescriptors);
        NSLog(@"[ControlStudioWidgetBackground] adapter-installed");
        CSRecordWidgetBackgroundEvent(@"install", @"installed", @"");
    } @catch (NSException *exception) {
        (void)exception;
        NSLog(@"[ControlStudioWidgetBackground] adapter-install-fallback");
        CSRecordWidgetBackgroundEvent(@"install", @"exception-caught", @"");
    } @finally {
        os_unfair_lock_unlock(&CSInstallLock);
    }
}
#endif

void CSInstallWidgetBackgroundAdapter(void) {
#if TARGET_OS_IOS
    CSStartWidgetBackgroundDiagnostics();
    CSRecordWidgetBackgroundEvent(@"entry", @"widget-bundle-init", @"");
    // Deliberately bounded: unknown future OS versions retain normal rendering.
    if (NSProcessInfo.processInfo.operatingSystemVersion.majorVersion != 27) {
        CSRecordWidgetBackgroundEvent(@"install", @"unsupported-os-major", @""); return;
    }
    CSInstallIfReady();
    static dispatch_once_t retry;
    dispatch_once(&retry, ^{ dispatch_async(dispatch_get_main_queue(), ^{ CSInstallIfReady(); }); });
#endif
}
