GUI Migration Plan
===================

Goal: provide a cross-platform GUI that reproduces the Windows app features on Windows and Linux.

Recommendation: use Avalonia UI
- Why: mature, XAML-based, supports Windows, Linux, macOS. Easier port from WinForms with a rewrite than MAUI in some cases. Good community, theming, and controls.

High-level steps:
1. Choose framework and scaffold project
   - Create `HASS.Agent.UI.Avalonia` project.
   - Reuse existing `HASS.Agent.Core` and shared libraries.

2. Port main windows and dialogs
   - Recreate `Main` window and core configuration pages (Onboarding, QuickActions, Tray, Media, MQTT, Sensors).
   - Migrate localization resource files into Avalonia resource format (resx can still be used via resource managers).

3. Replace Syncfusion controls
   - Use Avalonia's built-in controls and community packages for advanced controls.
   - Reimplement MessageBox styling using Avalonia dialogs.

4. Provide WebView support
   - On Windows keep WebView2 for advanced web content.
   - On Linux use WebView2GTK or open a browser for preview; consider embedding CefGlue or WebKit if needed.

5. Tray & Hotkeys
   - Use cross-platform tray library for Avalonia (e.g., TrayIcon plugin) and provide platform-specific hotkey implementations or delegate to headless API.

6. Testing & Packaging
   - Add CI builds for Windows and Linux; package installers for both platforms.

7. Iteration & QA
   - Maintain feature parity checklist and add tests for each feature.

Milestones and rough estimates
- Scaffolding + simple main window: 1-2 weeks
- Porting core configuration pages + localization: 2-3 weeks
- WebView + tray + hotkeys: 2-4 weeks (depends on platform complexity)
- Packaging and CI: 1 week
