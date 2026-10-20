// Hápticos mínimos de Project Ridge (POLISH_PASS 3.1) con UIImpactFeedbackGenerator.
// - RidgeHapticImpact(estilo): 0 leve, 1 medio, 2 fuerte.
// - RidgeHapticContinuous(on): zumbido continuo (impactos leves a ~25 Hz) mientras dura la captura del OVNI.
// UNVERIFIED hasta probarlo en un iPhone.
#import <UIKit/UIKit.h>

static NSTimer *ridgeHapticTimer = nil;
static UIImpactFeedbackGenerator *ridgeHapticBuzz = nil;

extern "C" void RidgeHapticImpact(int style)
{
    UIImpactFeedbackStyle s = style <= 0 ? UIImpactFeedbackStyleLight : (style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy);
    UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [g prepare];
    [g impactOccurred];
}

extern "C" void RidgeHapticContinuous(bool on)
{
    if (ridgeHapticTimer != nil) { [ridgeHapticTimer invalidate]; ridgeHapticTimer = nil; }
    if (!on) { ridgeHapticBuzz = nil; return; }
    ridgeHapticBuzz = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleRigid];
    [ridgeHapticBuzz prepare];
    ridgeHapticTimer = [NSTimer scheduledTimerWithTimeInterval:0.04 repeats:YES block:^(NSTimer *t) {
        [ridgeHapticBuzz impactOccurredWithIntensity:0.6];
        [ridgeHapticBuzz prepare];
    }];
}
