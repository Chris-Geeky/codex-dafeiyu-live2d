# 大肥鱼助手头像

头像取自随包提供的鲸鱼娘 Live2D 模型的睁眼帧。程序从透明渲染帧裁取头部，保留两侧耳鳍、原模型的发饰和五官。面板、任务栏、托盘和快捷方式使用同一来源。

角色原作上善无形，二次设计 ZipZipPipe，模型、绑定和表情氵六青。模型来自 A8Chann/dsh-pet-live2d 的固定提交 053f0837284c4997634842e77906c083b060f9d1。头像及 PNG/ICO 导出采用 CC BY-NC-SA 4.0；参见根目录 NOTICE 和模型 LICENSE。

生成：准备好已接受许可的 Live2D Core 后，运行 node scripts/capture-branding.mjs。将生成的 tests/.artifacts/model-branding/app-avatar.png 放入本目录，再运行 node scripts/build-branding.mjs --refresh-small。16–64 像素固定帧采用原有的轻锐化并保留透明边缘；其他尺寸从固定帧或同一张头像导出。两个脚本都在独立 Edge 中运行，不连接 Codex。
