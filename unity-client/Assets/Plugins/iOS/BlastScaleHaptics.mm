// BlastScaleHaptics.mm — Taptic Engine bridge for the Unity client.
//
// Unity calls the four extern "C" functions at the bottom (see Assets/Scripts/Core/Haptics.cs,
// DllImport("__Internal")). The UIKit feedback generators are created once, kept in static
// variables and re-prepared after every use, so each thump fires with minimal latency.
// Requires iOS 13 or newer (UIImpactFeedbackStyleSoft / Rigid); Unity 6 already targets iOS 13+.
// The file compiles with or without ARC: the generators are deliberate process-lifetime
// singletons, so nothing is ever released.

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

// One generator per impact style. The order matches the C# enum HapticImpact
// (0 light, 1 medium, 2 heavy, 3 soft, 4 rigid).
static UIImpactFeedbackGenerator *gImpactLight = nil;
static UIImpactFeedbackGenerator *gImpactMedium = nil;
static UIImpactFeedbackGenerator *gImpactHeavy = nil;
static UIImpactFeedbackGenerator *gImpactSoft = nil;
static UIImpactFeedbackGenerator *gImpactRigid = nil;
static UINotificationFeedbackGenerator *gNotification = nil;
static UISelectionFeedbackGenerator *gSelection = nil;
static BOOL gCreated = NO;

/// Creates every generator once. Safe to call from any of the entry points.
static void BlastScaleHapticEnsureCreated(void)
{
    if (gCreated) {
        return;
    }
    gCreated = YES;
    gImpactLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
    gImpactMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
    gImpactHeavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
    gImpactSoft = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleSoft];
    gImpactRigid = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleRigid];
    gNotification = [[UINotificationFeedbackGenerator alloc] init];
    gSelection = [[UISelectionFeedbackGenerator alloc] init];
}

/// Maps the C# enum value to its generator; unknown values fall back to medium.
static UIImpactFeedbackGenerator *BlastScaleHapticImpactGenerator(int style)
{
    switch (style) {
        case 0: return gImpactLight;
        case 1: return gImpactMedium;
        case 2: return gImpactHeavy;
        case 3: return gImpactSoft;
        case 4: return gImpactRigid;
        default: return gImpactMedium;
    }
}

/// UIKit wants feedback generators on the main thread; Unity's scripting thread is the main
/// thread on iOS, but the guard keeps the bridge safe if that ever changes.
static void BlastScaleHapticOnMainThread(void (^block)(void))
{
    if ([NSThread isMainThread]) {
        block();
    } else {
        dispatch_async(dispatch_get_main_queue(), block);
    }
}

extern "C" {

/// Warms every generator up (call when a level starts so the first pop has no latency).
void BlastScaleHapticPrepare(void)
{
    BlastScaleHapticOnMainThread(^{
        BlastScaleHapticEnsureCreated();
        [gImpactLight prepare];
        [gImpactMedium prepare];
        [gImpactHeavy prepare];
        [gImpactSoft prepare];
        [gImpactRigid prepare];
        [gNotification prepare];
        [gSelection prepare];
    });
}

/// One thump: style 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid.
void BlastScaleHapticImpact(int style)
{
    BlastScaleHapticOnMainThread(^{
        BlastScaleHapticEnsureCreated();
        UIImpactFeedbackGenerator *generator = BlastScaleHapticImpactGenerator(style);
        [generator impactOccurred];
        [generator prepare];
    });
}

/// Outcome feedback: type 0 success, 1 warning, 2 error (UINotificationFeedbackType order).
void BlastScaleHapticNotification(int type)
{
    BlastScaleHapticOnMainThread(^{
        BlastScaleHapticEnsureCreated();
        UINotificationFeedbackType feedbackType;
        switch (type) {
            case 0: feedbackType = UINotificationFeedbackTypeSuccess; break;
            case 1: feedbackType = UINotificationFeedbackTypeWarning; break;
            default: feedbackType = UINotificationFeedbackTypeError; break;
        }
        [gNotification notificationOccurred:feedbackType];
        [gNotification prepare];
    });
}

/// The faint tick of a selection change (button presses).
void BlastScaleHapticSelection(void)
{
    BlastScaleHapticOnMainThread(^{
        BlastScaleHapticEnsureCreated();
        [gSelection selectionChanged];
        [gSelection prepare];
    });
}

} // extern "C"
