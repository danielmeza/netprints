# Visuals, animation and layout

Animations, transitions, composition effects, theme variants, cursors and responsive classes. NetPrints rarely needs these.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Contents

- Catalog: Composition (Animations); Animations; Composition (Custom); Transitions; ThemeVariant; Cursor; Icon; Layout; RenderTarget; WriteableBitmap; Screen; Multimedia; Responsive

## Catalog

### Composition · `Xaml.Behaviors.Animations` (package not referenced)

Use when: Composition-layer selection animation on an items control [not needed].

| Name | Kind | What it does |
|---|---|---|
| `SelectingItemsControlBehavior` | Behavior | Enables the standard selection indicator animation on selecting items controls. |

### Animations · `Xaml.Behaviors.Interactions.Custom`

Use when: Start/await animations from triggers [use for view polish only].

| Name | Kind | What it does |
|---|---|---|
| `AnimateOnAttachedBehavior` | Behavior | Runs an animation when the associated control is attached to the visual tree. |
| `AnimationCompletedTrigger` | Trigger | Runs a specified Animation.Animation and executes actions when it completes. |
| `BeginAnimationAction` | Action | Starts an Animation.Animation on a specified control when executed. |
| `FadeInBehavior` | Behavior | Plays a simple fade in animation when the associated control is attached. |
| `PlayAnimationBehavior` | Behavior | Plays a specified Animation when the associated element is attached to the visual tree. |
| `RunAnimationTrigger` | Trigger | Runs an animation and invokes actions when the associated control is attached to the visual tree. |
| `StartAnimationAction` | Action | Starts an Animation.Animation on the associated control. |
| `StartBuiltAnimationAction` | Action | Starts an animation built in code on the associated control. |

### Composition · `Xaml.Behaviors.Interactions.Custom`

Use when: Decorative composition effects [not needed].

| Name | Kind | What it does |
|---|---|---|
| `OrbitEffectBehavior` | Behavior | A behavior that allows rotating the attached control in 3D space using pointer manipulation. |
| `ParallaxBehavior` | Behavior | A behavior that moves the associated element at a different speed than the scrolling container, creating a parallax effect. |
| `TiltEffectBehavior` | Behavior | A behavior that applies a 3D tilt rotation to the element based on the pointer position. |

### Transitions · `Xaml.Behaviors.Interactions.Custom`

Use when: Manage Transitions from triggers [view polish].

| Name | Kind | What it does |
|---|---|---|
| `AddTransitionAction` | Action | Adds a TransitionBase to the Avalonia.Animation.Transitions collection on the target element. |
| `ClearTransitionsAction` | Action | Clears the Avalonia.Animation.Transitions collection. |
| `RemoveTransitionAction` | Action | Removes a TransitionBase from the Avalonia.Animation.Transitions collection on the target element. |
| `TransitionsBehavior` | Behavior | Sets the Avalonia.Animation.Transitions collection on the associated control when attached. |
| `TransitionsChangedTrigger` | Trigger | Executes actions whenever the Avalonia.Animation.Transitions collection changes. |

### ThemeVariant · `Xaml.Behaviors.Interactions.Custom`

Use when: Set or react to theme variant per subtree [prefer ThemeVariantScope + DynamicResource].

| Name | Kind | What it does |
|---|---|---|
| `ThemeVariantBehavior` | Behavior | Sets the ThemeVariantScope.RequestedThemeVariant on the associated control. |
| `ThemeVariantTrigger` | Trigger | Executes actions when the associated control's StyledElement.ActualThemeVariant matches the specified ThemeVariant. |

### Cursor · `Xaml.Behaviors.Interactions.Custom`

Use when: Set cursors on hover or from a provider [view-only cursor changes].

| Name | Kind | What it does |
|---|---|---|
| `PointerOverCursorBehavior` | Behavior | Changes the cursor when the pointer is over the associated control. |
| `SetCursorAction` | Action | Sets the cursor on a target control. |
| `SetCursorBehavior` | Behavior | Sets the cursor for the associated control when attached. |
| `SetCursorFromProviderAction` | Action | Sets the cursor on a target control using an ICursorProvider. |
| `SetCursorFromProviderBehavior` | Behavior | Sets the cursor provided by an ICursorProvider when attached. |

### Icon · `Xaml.Behaviors.Interactions.Custom`

Use when: PathIcon data swapping [not used; Material.Icons instead].

| Name | Kind | What it does |
|---|---|---|
| `PathIconDataBehavior` | Behavior | Sets the PathIcon.Data when the associated icon is attached to the visual tree. |
| `PathIconDataChangedTrigger` | Trigger | Invokes actions whenever the PathIcon.Data property changes. |
| `SetPathIconDataAction` | Action | Changes the PathIcon.Data of a target icon when executed. |

### Layout · `Xaml.Behaviors.Interactions.Custom`

Use when: Fluid move animations / reparenting [not needed].

| Name | Kind | What it does |
|---|---|---|
| `FluidMoveBehavior` | Behavior | Behavior that animates position changes of a control or its children. |
| `MoveElementToPanelAction` | Action | Moves the associated or target element to a specified Panel. |

### RenderTarget · `Xaml.Behaviors.Interactions.Custom`

Use when: Render a control to a bitmap [screenshots/exports only].

| Name | Kind | What it does |
|---|---|---|
| `RenderRenderTargetBitmapAction` | Action | Action that invokes IRenderTargetBitmapRenderHost.Render on the specified target. |
| `RenderTargetBitmapBehavior` | Behavior | Behavior that draws into a RenderTargetBitmap and assigns it to the associated Image. |
| `RenderTargetBitmapTrigger` | Trigger | Trigger that calls IRenderTargetBitmapRenderHost.Render periodically. |
| `StaticRenderTargetBitmapBehavior` | Behavior | Behavior that draws once into a RenderTargetBitmap and assigns it to the associated Image. Rendering can be triggered by calling IRenderTargetBitmapRenderHost.Render. |

### WriteableBitmap · `Xaml.Behaviors.Interactions.Custom`

Use when: Render into WriteableBitmap on a timer [not needed].

| Name | Kind | What it does |
|---|---|---|
| `WriteableBitmapBehavior` | Behavior | Creates a WriteableBitmap and optionally renders it once using a renderer. |
| `WriteableBitmapRenderAction` | Action | Invokes an IWriteableBitmapRenderer to render into a bitmap. |
| `WriteableBitmapRenderBehavior` | Behavior | Creates a WriteableBitmap and updates it using a renderer on a timer. |
| `WriteableBitmapTimerTrigger` | Trigger | A trigger that fires its actions on a timer and passes a WriteableBitmap as parameter. |
| `WriteableBitmapTrigger` | Trigger | A trigger that executes its actions when Trigger is called, passing a WriteableBitmap as parameter. |

### Screen · `Xaml.Behaviors.Interactions.Custom`

Use when: Screen/monitor info [not needed].

| Name | Kind | What it does |
|---|---|---|
| `ActiveScreenBehavior` | Behavior | A behavior that exposes the screen containing the associated TopLevel. |
| `RequestScreenDetailsAction` | Action | An action that requests extended screen information from Screens. |
| `ScreensChangedTrigger` | Trigger | A trigger that executes its actions when the screen configuration changes. |

### Multimedia · `Xaml.Behaviors.Interactions.Custom`

Use when: Console beep [no].

| Name | Kind | What it does |
|---|---|---|
| `ConsoleBeepAction` | Action | An action that plays a system beep. |

### Responsive · `Xaml.Behaviors.Interactions.Responsive` (package not referenced)

Use when: Adaptive classes by width/aspect ratio.

| Name | Kind | What it does |
|---|---|---|
| `AdaptiveBehavior` | Behavior | Observes StyledElementBehavior{T}.AssociatedObject control or SourceControl control Visual.Bounds property changes and if triggered sets or removes style classes when conditions from AdaptiveClassSetter are met. |
| `AspectRatioBehavior` | Behavior | Observes bounds changes of a control (or a specified source) and conditionally adds or removes classes based on AspectRatioClassSetter rules. |
