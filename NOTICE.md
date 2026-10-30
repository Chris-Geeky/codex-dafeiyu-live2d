# 来源与许可

本项目是非官方的 Codex 宠物窗口适配器，与 OpenAI、DeepSeek、Live2D 及原模型作者没有隶属关系。它只接入这一套固定模型，没有通用模型导入功能。

## 角色和模型

- 角色原作：**上善无形**，原创 OC「溟月」。
- DeepSeek 女仆鲸鱼娘二次设计：**ZipZipPipe**。
- Live2D 模型、绑定、表情和动作：**氵六青**。
- 获取来源：[A8Chann/dsh-pet-live2d](https://github.com/A8Chann/dsh-pet-live2d/tree/053f0837284c4997634842e77906c083b060f9d1)，固定提交 `053f0837284c4997634842e77906c083b060f9d1`。
- `assets/model/` 中的美术采用 [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/)，原许可与作者链接完整保留在 [assets/model/LICENSE](assets/model/LICENSE)。这些素材没有修改。
- 分享需要保留署名和许可；非商业条件仍适用于模型。修改后的美术按相同许可共享。顶层 MIT 许可不覆盖这些素材。

## 代码和运行库

适配器与启动器代码使用 MIT；参考的 A8Chann MIT 代码及 Pixi、渲染引擎、Cubism Framework 许可保留在 `third-party/`。具体版本和构建方法见 [third-party/SOURCES.md](third-party/SOURCES.md)。

`assets/branding/app-avatar.png` 是以同一来源的鲸鱼娘头像为参照做的图标化改编，移除预览中的鼠标提示并补全头像，保留蓝发、女仆头饰和头顶小鲸鱼。头像与其导出的 PNG/ICO 沿用角色美术的 **CC BY-NC-SA 4.0**，署名仍为上善无形、ZipZipPipe、氵六青；头像不受顶层 MIT 许可覆盖，也不是 DeepSeek 或 OpenAI 的官方标志。改编记录见 `assets/branding/README.md`。

**Live2D Cubism Core 是专有组件，未放入本仓库或便携压缩包。** 首次运行时，接收者阅读并同意 [Live2D Proprietary Software License Agreement](https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html) 后，从 Live2D 官方 CDN 下载固定哈希的文件。保留其原文件头。此分离方式不表示使用或发布不再受 SDK 条款约束。

SDK 的使用和发行仍适用 [Live2D SDK Release License](https://www.live2d.com/en/sdk/license/)；其个人、小规模主体豁免有条件，Expandable Application 等情形另有要求。改变为通用模型平台、商业发行或其他超出本项目固定模型个人使用的用途时，请向相应权利人确认许可。

便携包中的 Node.js 来自官方 Windows x64 发行压缩包，SHA-256 与官方 `SHASUMS256.txt` 比对，其全部许可保留在 `runtime/Node-LICENSE.txt`。
