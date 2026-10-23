#import "include/CSWidgetBackgroundAdapter.h"
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
// The iOS 27 device exposes the signed integer encoding q, not Q.
- (void)setPreferredBackgroundStyle:(NSInteger)value;
@end

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
    free(returns);
    if (argumentType) {
        char *argument = method_copyArgumentType(method, 2);
        // Apple BOOL is encoded as B or c depending on architecture. Both
        // occupy one byte; accept either for the two Boolean setters only.
        BOOL boolSetter = selector == @selector(setBackgroundRemovable:)
            || selector == @selector(setTransparent:);
        BOOL compatible = argument && (strcmp(argument, argumentType) == 0
            || (boolSetter && (strcmp(argument, "B") == 0 || strcmp(argument, "c") == 0)));
        matches = matches && compatible;
        free(argument);
    }
    return matches;
}

@interface CSBackgroundDecodeDelegate : NSObject <NSKeyedUnarchiverDelegate>
@property(nonatomic) Class descriptorClass;
@property(nonatomic) BOOL failed;
@property(nonatomic) NSUInteger patchedCount;
@end

@implementation CSBackgroundDecodeDelegate
- (id)unarchiver:(NSKeyedUnarchiver *)unarchiver didDecodeObject:(id)object {
    if (![object isKindOfClass:self.descriptorClass]) return object;
    if (!CSMethodMatches(object, @selector(kind), @encode(id), NULL)) {
        self.failed = YES; return object;
    }
    id<CSDescriptorBackgroundMethods> descriptor = object;
    NSString *kind = descriptor.kind;
    if (!CSIsStudioSlotKind(kind)) return object;
    if (![object conformsToProtocol:@protocol(NSMutableCopying)]) {
        self.failed = YES; return object;
    }
    id<CSDescriptorBackgroundMethods> copy = [object mutableCopy];
    if (!copy || copy == object || ![(id)copy isKindOfClass:self.descriptorClass]) {
        self.failed = YES; return object;
    }
    for (NSString *name in @[@"setBackgroundRemovable:", @"setTransparent:", @"setPreferredBackgroundStyle:"]) {
        SEL setter = NSSelectorFromString(name);
        const char *type = setter == @selector(setPreferredBackgroundStyle:) ? @encode(NSInteger) : @encode(BOOL);
        if (!CSMethodMatches(copy, setter, @encode(void), type)) {
            self.failed = YES; return object;
        }
    }
    [copy setBackgroundRemovable:YES];
    [copy setTransparent:YES];
    [copy setPreferredBackgroundStyle:1]; // Clear request; system chrome is device-verified separately.
    self.patchedCount++;
    return copy;
}
@end

id CSWidgetBackgroundRewriteResult(id result, Class descriptorClass) {
    if (!result || !descriptorClass || ![result conformsToProtocol:@protocol(NSSecureCoding)]) return result;
    @try {
        NSError *error = nil;
        NSData *data = [NSKeyedArchiver archivedDataWithRootObject:result requiringSecureCoding:YES error:&error];
        if (error || !data || data.length > 8 * 1024 * 1024) return result;
        NSKeyedUnarchiver *decoder = [[NSKeyedUnarchiver alloc] initForReadingFromData:data error:&error];
        if (!decoder || error) return result;
        decoder.requiresSecureCoding = YES;
        decoder.decodingFailurePolicy = NSDecodingFailurePolicySetErrorAndReturn;
        CSBackgroundDecodeDelegate *rewrite = [CSBackgroundDecodeDelegate new];
        rewrite.descriptorClass = descriptorClass;
        decoder.delegate = rewrite;
        id updated = [decoder decodeObjectOfClass:[result class] forKey:NSKeyedArchiveRootObjectKey];
        [decoder finishDecoding];
        if (!updated || decoder.error || rewrite.failed || !rewrite.patchedCount || ![updated isKindOfClass:[result class]]) return result;
        return updated;
    } @catch (NSException *exception) {
        (void)exception;
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
    original(object, selector, ^(id result) {
        @autoreleasepool {
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
        if (!exported || !descriptor || !method || method_getNumberOfArguments(method) != 3) return;
        char *returns = method_copyReturnType(method);
        char *argument = method_copyArgumentType(method, 2);
        BOOL compatible = returns && argument && strcmp(returns, @encode(void)) == 0 && strcmp(argument, "@?") == 0;
        free(returns); free(argument);
        if (!compatible) return;
        IMP original = method_getImplementation(method);
        if (!original || original == (IMP)CSFetchDescriptors) return;
        atomic_store_explicit(&CSOriginalFetch, original, memory_order_release);
        method_setImplementation(method, (IMP)CSFetchDescriptors);
    } @catch (NSException *exception) {
        (void)exception;
    } @finally {
        os_unfair_lock_unlock(&CSInstallLock);
    }
}
#endif

void CSInstallWidgetBackgroundAdapter(void) {
#if TARGET_OS_IOS
    // Deliberately bounded: unknown future OS versions retain normal rendering.
    if (NSProcessInfo.processInfo.operatingSystemVersion.majorVersion != 27) return;
    CSInstallIfReady();
    static dispatch_once_t retry;
    dispatch_once(&retry, ^{ dispatch_async(dispatch_get_main_queue(), ^{ CSInstallIfReady(); }); });
#endif
}
