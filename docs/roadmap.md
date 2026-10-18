# Future milestones — plan only

**Do not begin Milestone 1 until the owner accepts Milestone 0 and explicitly approves further work.**

Milestone 1 would add the first Shortcut Control Center actions: validated HTTP(S)/explicitly allowed deep links, clipboard text and internal counter commands. Introduce stable IDs, typed parameters and `IActionDefinition`, `IActionExecutor`, `IActionRepository`, `IActionValidator`, with cancellation, validation and user-visible failures. Store dashboard records in local SQLite; plan categories, favorites, ordering, names/icons/colors and accessible tile rendering. Implement a small complete action flow before adding drag-and-drop, image selection or advanced utilities.

Later actions may include timestamps, Base64, local notes and an in-app focus timer. Apple Shortcuts can expose supported typed App Intent parameters; investigate App Entities/queries for user configurations. Documented Shortcuts URL mechanisms must validate and encode names/inputs. Do not treat bundle IDs as launchable URLs or run untrusted/downloaded scripts.

| Execution surface | Planned boundaries |
|---|---|
| MAUI foreground | Validated URLs/deep links, clipboard, notes, utilities, in-app commands |
| Apple Shortcuts | Statically declared/discoverable App Intents with safe typed parameters; shared state actions |
| Interactive WidgetKit | Only eligible `Button(intent:)`/Toggle operations and supported navigation |
| Foreground-required | UI prompts, certain URL navigation, operations requiring user interaction |
| Unsupported background | Arbitrary scripts, unrestricted app launching, continuous timers or monitoring |

Widget Studio comes after the integration proof. Define a versioned, bounded JSON layout tree with whitelisted text/date/progress/icon/image/stack components, padding/alignment/background/theme tokens, maximum depth/count/text length and validated local asset references. SwiftUI renders the native layout; MAUI supplies an approximate preview. Add explicit migration and shared snapshot contracts. Never execute layout JSON as code or embed MAUI views into WidgetKit.

Start with custom text, countdown/date and progress/savings/daily-goal templates. Later investigate calendar, quote, checklist, battery data through permitted APIs and supported quick-action navigation. Small/medium are established by Milestone 0; investigate large and accessory Lock Screen families for the device before adding them. Respect timeline budgets for clock/countdown rendering and data access. Build theme/light-dark variations and presets before a general layout editor.

Before each milestone, define acceptance tests covering input validation, repository persistence, native contract compatibility, permissions/signing changes, background limits and actual device behavior. Record MAUI preview/native SwiftUI differences and provide truthful unsupported states.
