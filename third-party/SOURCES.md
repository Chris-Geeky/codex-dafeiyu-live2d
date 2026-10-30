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
