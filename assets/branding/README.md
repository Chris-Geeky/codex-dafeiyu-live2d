# 鲸鱼娘程序头像

来源：A8Chann/dsh-pet-live2d 固定提交 `053f0837284c4997634842e77906c083b060f9d1` 中的 `pets/ds-whale-girl/previews/idle.png`。

原角色上善无形，二次设计 ZipZipPipe，模型／绑定／表情氵六青。原美术许可为 CC BY-NC-SA 4.0。本头像及 PNG/ICO 导出沿用该许可，保留署名、非商业、相同方式共享条件；详情见根目录 NOTICE 和模型 LICENSE。

`app-avatar.png` 是通过内置图像编辑工具，以原头像为参照移除鼠标提示、补全右侧头发与头饰、增加透明留白后的头像改编。没有更换角色模型。`app-256.png` 从该头像导出。

生成提示：

> Edit the provided original whale-girl avatar: remove the large blue computer mouse badge on the right and reconstruct the covered hair, face and maid headband. Preserve the same large blue eyes, periwinkle blue hair, side bows, white frilled maid headdress, rosy cheeks, smiling mouth, and tiny light-blue whale on her head. Fit the complete head in a square with a small transparent margin; only a hint of collar. Keep clear dark linework and flat cel shading, readable at small app icon sizes. Transparent background, no text, border, solid tile, watermark or added UI elements.

`app-avatar.png` 保持原文件不变，SHA-256 为 `f5579f9ae4a6e08faba240f844ec8931751a7584174a8cdc1d0e605ff5751541`。所有尺寸来自同一原图，没有重画五官、耳朵或头饰。

小尺寸采用实际像素对照中选定的 B 档轻锐化：高质量缩小后，对不透明度至少 128 的像素进行 RGB 局部对比增强（3×3 高斯权重、锐化量 1.1、对比度与饱和度各 1.02），保留原 alpha。算法记录在 `scripts/icon-renderer.mjs`。16、24、32、48、64 像素的确认结果保存为 `native-*.png`；浏览器采样路径可能产生差异，因此这些固定帧直接装入 ICO，不重复生成。其他小尺寸从较大的确认帧缩小，不叠加锐化；大于 64 像素从原头像导出，不锐化。

使用 `node scripts/build-branding.mjs`，通过本机 Edge 组装 15 种 ICO 尺寸（16–256 像素）并更新资源哈希。窗口与托盘按 DPI 选择相应帧。工具只做本机渲染与格式转换，不调用图像生成服务。可加 `--compare-with path/to/old.ico` 生成实际像素对照。
