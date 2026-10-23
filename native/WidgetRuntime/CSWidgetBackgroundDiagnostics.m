#import "include/CSWidgetBackgroundDiagnostics.h"
#import <TargetConditionals.h>
#import <dispatch/dispatch.h>

@interface CSWidgetBackgroundDiagnosticRecorder ()
@property(nonatomic, strong) NSURL *directory;
@property(nonatomic, strong) NSURL *file;
@property(nonatomic, strong) NSDictionary *metadata;
@property(nonatomic, strong) NSMutableArray *events;
@property(nonatomic, strong) NSMutableDictionary *latest;
@property(nonatomic, strong) NSLock *lock;
@property(nonatomic, strong) dispatch_queue_t queue;
@property(nonatomic) BOOL pending;
@property(nonatomic) NSUInteger sequence;
@end

static NSString *CSTimestamp(void) {
    return [[NSISO8601DateFormatter new] stringFromDate:[NSDate date]];
}

static NSString *CSBoundedText(NSString *value) {
    // Static status/ABI data only. Keep accidental multiline strings out of reports.
    NSString *singleLine = [[value componentsSeparatedByCharactersInSet:NSCharacterSet.controlCharacterSet] componentsJoinedByString:@" "];
    return singleLine.length > 256 ? [singleLine substringToIndex:256] : singleLine;
}

@implementation CSWidgetBackgroundDiagnosticRecorder
- (instancetype)initWithDirectory:(NSURL *)directory version:(NSString *)version build:(NSString *)build os:(NSString *)os {
    if ((self = [super init])) {
        _directory = directory;
        NSString *session = NSUUID.UUID.UUIDString;
        _file = [directory URLByAppendingPathComponent:[session stringByAppendingString:@".json"]];
        _metadata = @{@"schema": @1, @"session": session, @"version": CSBoundedText(version),
            @"build": CSBoundedText(build), @"os": CSBoundedText(os), @"startedUtc": CSTimestamp()};
        _events = [NSMutableArray new]; _latest = [NSMutableDictionary new]; _lock = [NSLock new];
        _queue = dispatch_queue_create("com.controlstudio.widget-background-diagnostics", DISPATCH_QUEUE_SERIAL);
    }
    return self;
}
- (void)recordStage:(NSString *)stage reason:(NSString *)reason detail:(NSString *)detail {
    @try {
        [self.lock lock];
        @try {
            NSDictionary *event = @{@"sequence": @(++self.sequence), @"utc": CSTimestamp(),
                @"stage": CSBoundedText(stage), @"reason": CSBoundedText(reason), @"detail": CSBoundedText(detail)};
            [self.events addObject:event]; self.latest[CSBoundedText(stage)] = event;
            if (self.latest.count > 12) {
                NSString *oldest = [[self.latest allKeys] sortedArrayUsingComparator:^NSComparisonResult(NSString *a, NSString *b) {
                    return [self.latest[a][@"sequence"] compare:self.latest[b][@"sequence"]];
                }].firstObject;
                if (oldest) [self.latest removeObjectForKey:oldest];
            }
            if (self.events.count > 64) [self.events removeObjectAtIndex:0];
            if (self.pending) return;
            self.pending = YES;
            dispatch_async(self.queue, ^{
                @autoreleasepool {
                    @try { [self writeSnapshot]; }
                    @catch (NSException *exception) { (void)exception; }
                }
            });
        } @finally { [self.lock unlock]; }
    } @catch (NSException *exception) { (void)exception; }
}
- (void)writeSnapshot {
    [self.lock lock];
    NSMutableDictionary *snapshot;
    @try {
        snapshot = [self.metadata mutableCopy]; snapshot[@"events"] = [self.events copy];
        snapshot[@"latest"] = [self.latest copy];
        self.pending = NO;
    } @finally { [self.lock unlock]; }
    NSError *error = nil;
    NSFileManager *files = NSFileManager.defaultManager;
    if (![files createDirectoryAtURL:self.directory withIntermediateDirectories:YES attributes:nil error:&error]) return;
    NSData *data = [NSJSONSerialization dataWithJSONObject:snapshot options:0 error:&error];
    if (!data || error || data.length > 65536) return;
    NSDataWritingOptions options = NSDataWritingAtomic;
#if TARGET_OS_IOS
    options |= NSDataWritingFileProtectionCompleteUntilFirstUserAuthentication;
#endif
    if (![data writeToURL:self.file options:options error:&error]) return;
    // Prune only UUID.json files in this dedicated diagnostics folder.
    NSArray<NSURL *> *existing = [files contentsOfDirectoryAtURL:self.directory
        includingPropertiesForKeys:@[NSURLContentModificationDateKey] options:NSDirectoryEnumerationSkipsHiddenFiles error:nil];
    NSMutableArray<NSURL *> *sessions = [NSMutableArray new];
    for (NSURL *url in existing) {
        if ([url.pathExtension isEqualToString:@"json"] && [[NSUUID alloc] initWithUUIDString:url.lastPathComponent.stringByDeletingPathExtension])
            [sessions addObject:url];
    }
    [sessions sortUsingComparator:^NSComparisonResult(NSURL *a, NSURL *b) {
        if ([a isEqual:b]) return NSOrderedSame;
        if ([a isEqual:self.file]) return NSOrderedAscending;
        if ([b isEqual:self.file]) return NSOrderedDescending;
        NSDate *left = nil, *right = nil;
        [a getResourceValue:&left forKey:NSURLContentModificationDateKey error:nil];
        [b getResourceValue:&right forKey:NSURLContentModificationDateKey error:nil];
        return [(right ?: NSDate.distantPast) compare:(left ?: NSDate.distantPast)];
    }];
    for (NSUInteger i = 16; i < sessions.count; i++) {
        if (![sessions[i] isEqual:self.file]) [files removeItemAtURL:sessions[i] error:nil];
    }
}
- (void)flush { dispatch_sync(self.queue, ^{}); }
@end

static CSWidgetBackgroundDiagnosticRecorder *CSRecorder;
void CSStartWidgetBackgroundDiagnostics(void) {
#if TARGET_OS_IOS
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        @try {
            NSBundle *bundle = NSBundle.mainBundle;
            NSString *group = [bundle objectForInfoDictionaryKey:@"ControlStudioAppGroup"];
            if (![group isKindOfClass:NSString.class] || ![group hasPrefix:@"group."]) return;
            NSURL *container = [NSFileManager.defaultManager containerURLForSecurityApplicationGroupIdentifier:group];
            if (!container) { NSLog(@"[ControlStudioWidgetBackground] diagnostics-group-unavailable"); return; }
            NSURL *directory = [container URLByAppendingPathComponent:@"Library/Application Support/ControlStudio/WidgetBackgroundDiagnostics" isDirectory:YES];
            CSRecorder = [[CSWidgetBackgroundDiagnosticRecorder alloc] initWithDirectory:directory
                version:[bundle objectForInfoDictionaryKey:@"CFBundleShortVersionString"] ?: @"unknown"
                build:[bundle objectForInfoDictionaryKey:@"CFBundleVersion"] ?: @"unknown"
                os:NSProcessInfo.processInfo.operatingSystemVersionString];
        } @catch (NSException *exception) { (void)exception; }
    });
#endif
}
void CSRecordWidgetBackgroundEvent(NSString *stage, NSString *reason, NSString *detail) {
    [CSRecorder recordStage:stage reason:reason detail:detail];
}
