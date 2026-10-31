# 第三方来源

- 模型与渲染入口参考：A8Chann/dsh-pet-live2d，固定提交 `053f0837284c4997634842e77906c083b060f9d1`。
  - https://github.com/A8Chann/dsh-pet-live2d/tree/053f0837284c4997634842e77906c083b060f9d1
  - 原代码许可和美术许可说明分别保留在 `A8Chann-code-LICENSE.txt` 与 `A8Chann-NOTICE.md`。
- PixiJS 8.19.0，MIT。
  - https://github.com/pixijs/pixijs/blob/v8.19.0/LICENSE
  - 当前 vendor 增加官方 `pixi.js/unsafe-eval` 兼容模块，以普通函数替代动态代码生成，保持宿主 CSP 不变：https://github.com/pixijs/pixijs/blob/v8.19.0/src/unsafe-eval/init.ts
- untitled-pixi-live2d-engine 1.3.5，MIT；与上述固定 A8Chann 提交使用相同依赖版本，本地 vendor 经 esbuild 0.25.10 重新构建。
  - https://github.com/Untitled-Story/untitled-pixi-live2d-engine
  - 许可证副本来源：https://github.com/Untitled-Story/untitled-pixi-live2d-engine/blob/master/LICENSE
- Cubism Web Framework 的声明与使用条件保留在 `CubismWebFramework-LICENSE.md`。
  - https://github.com/Live2D/CubismWebFramework/blob/develop/LICENSE.md
- Live2D Cubism Core：首次使用经接收者同意后从官方 CDN 获取，不包含于仓库或便携包，文件哈希列于 `assets-manifest.json`，保留原文件头。
  - https://cubism.live2d.com/sdk-web/cubismcore/live2dcubismcore.min.js
  - https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html

模型、贴图、动作与表情没有改动；Codex 适配器、窗口布局和本地验证文件为本次新增。上述代码许可不替代角色美术的 CC BY-NC-SA 4.0 条件，也不替代 Live2D 组件各自的许可。

动作调度的生命周期、`loop:false`、自拍前置动作和道具参数复位参考了上述 A8Chann 固定提交的 MIT 代码 `lib/client.js`；Codex 的调度、自然眨眼、冷却和窗口留白在本适配器中实现。

本地构建入口和锁定依赖位于本仓库 `build/`，包获取使用 `npm install --ignore-scripts --no-audit --no-fund`；`assets-manifest.json` 记录实际产物哈希。可运行 `node scripts/build-vendor.mjs` 重新构建。

- Microsoft.Web.WebView2 SDK **1.0.3537.50**，来自微软官方 NuGet 包：
  - https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3537.50
  - 原包 SHA-256：`5ea526bbd728adda0da4d31219267e96460494a427e4894c4e09d9f320f4b9aa`。
  - 仅保留 net462 的 Core / WinForms DLL 和 Windows x64 WebView2Loader；文件固定哈希列于资源清单。许可证与第三方声明见 `WebView2-LICENSE.txt`、`WebView2-NOTICE.txt`。
  - 实际浏览器运行库使用接收者系统上的 WebView2 Runtime，未随包分发；运行库不可用时使用基础装扮界面。
  - WinForms 接入依据：https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms
