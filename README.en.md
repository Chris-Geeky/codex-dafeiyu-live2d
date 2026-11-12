# Dafeiyu Live2D for Codex

[简体中文](README.md) · English

Dafeiyu is an unofficial Windows companion for the Codex desktop app. It animates the whale maid character in Codex’s existing pet window, with blinking, eye movement, task reactions, expressions, and outfits. The assistant does not replace the Codex app or change its installed files.

**[Download the Windows x64 portable preview](https://github.com/Chris-Geeky/codex-dafeiyu-live2d/releases)** · [Quick start](START-HERE.en.txt) · [Licenses and credits](NOTICE.md)

## Get started

1. Install and sign in to the official Codex desktop app on Windows x64. Select any pet in Codex and show its pet window.
2. Download the portable ZIP from the release’s **Assets** section and extract it to a folder you can keep. Run `CodexDafeiyu.exe` from that folder, with `assets` and `runtime` alongside it. Node.js is included.
3. If the first-run dialog opens in Chinese, click **暂不启用** (Not now). In the main panel, open **More options → Language** (`更多选项 → 语言`), select **English**, then click **Enable Dafeiyu**. Read the [Live2D runtime license](https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html). If you accept it, the assistant downloads the runtime from Live2D and verifies its SHA-256 hash. An internet connection is needed for this step.
4. If Codex was started without the local pet connection, click **Connect after restart**. Save your work, then use **Quit** in the **Codex** tray menu. Closing only the main window may leave Codex running. Leave the Dafeiyu assistant open and let it start Codex again.
5. Once the panel says **Connected to Codex** and Dafeiyu appears, you can choose **Minimize to tray**. The pet stays active.

For everyday use, start Codex through `CodexDafeiyu.exe` or a shortcut to it. If the assistant is already running, opening it again or double-clicking its tray icon shows the panel. A separate shortcut to `CodexDafeiyu.exe --codex-only` can start Codex with the local connection enabled and leave the pet unchanged until you open the assistant. That shortcut cannot add the connection to an already running Codex session.

## Make it yours

- Switch between 30 and 60 FPS targets and adjust the pet size from 60% to 130%.
- Use **Expressions & outfits…** to preview 20 expressions, outfits, accessories, and effects. Changes in the preview are only saved when you choose **Apply**.
- Choose a light, dark, or system theme. The interface supports English, Simplified Chinese, and a system language setting.
- If the main Codex window jumps during a move or resize, try **Window drag compatibility** or **Hide before dragging**. Moving the pet to the left edge of the screen may also help. This is a workaround for a Windows overlay issue; see the [drag notes (Chinese)](docs/windows-window-drag.md).

## Troubleshooting and compatibility

- **Waiting for Codex or the pet window:** Check the status message in the assistant. If the assistant is waiting for a pet window, select a pet and show it in Codex. If it is waiting for Codex to quit, follow step 4 above. See [startup and reconnection details (Chinese)](docs/startup-and-reconnect.md).
- **The preview will not open:** If WebView2 is unavailable, basic outfit controls remain available. The portable ZIP does not install WebView2 for you.
- **Windows flags a file:** SmartScreen reputation warnings and antivirus detections are different. This preview’s launcher is unsigned. If antivirus reports a threat, leave the file blocked and report the exact detection and release version. Do not share full logs or personal information.
- **Reporting another problem:** Open **More options → View logs**. In a [GitHub issue](https://github.com/Chris-Geeky/codex-dafeiyu-live2d/issues), include the assistant version, Codex version, Windows version, and the steps to reproduce. Share only the relevant log lines after removing personal paths, account details, and private links.

This preview targets Windows x64 and has been tested with specific versions of the Windows Store build of Codex. Other Codex builds may change the private interface used by the assistant. macOS, Linux, and Windows ARM64 are not supported.

## Restore, update, or uninstall

Choose **Restore original pet** or **Quit assistant** to stop Live2D and restore the original pet while Codex stays open. If the original image remains unavailable, retry or check the logs. To close the local connection itself, fully quit that Codex session and start it from the original official shortcut.

To update, quit the old assistant, extract the new ZIP into a separate folder, and start the new version. Keep the old folder until the new version works; to roll back, quit the new assistant before opening the old one. Settings are stored separately under `%LOCALAPPDATA%\CodexDafeiyuLive2D`. To uninstall, quit the assistant and remove its extracted folder. Remove the local settings folder only if you also want to clear preferences and cached files.

## How it connects

The assistant uses Codex’s local debug interface. It is not an official Live2D API. The port is bound to loopback and is not authenticated: other software running with comparable privileges on your computer may be able to access it while that Codex session is open. The assistant limits its own use to the pet window and its open/close controls. It does not read chat content or upload the model.

## Build from source

On Windows x64, the build uses PowerShell, curl, and the .NET Framework C# compiler. Run `npm test` with Node.js 22 or newer to check the source. Build the portable ZIP and its checksum with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Portable.ps1
```

The script verifies the bundled Node.js archive against the official SHA-256 list. Its output is written to `dist/`; use a fresh `-OutputDirectory` if you already have a build. The portable package does not contain the proprietary Cubism Core, account data, or local logs.

## Credits

The adapter code is [MIT](LICENSE). The original character is by **上善无形**, the whale maid redesign is by **ZipZipPipe**, and the Live2D model, rigging, expressions, and motions are by **氵六青**. The model and derived portrait are under [CC BY-NC-SA 4.0](assets/model/LICENSE), separate from the code license. Live2D components have their own terms. See [NOTICE](NOTICE.md) and the [third-party sources](third-party/SOURCES.md).
