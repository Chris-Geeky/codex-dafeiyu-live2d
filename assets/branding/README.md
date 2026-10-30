# 鲸鱼娘程序头像

来源：A8Chann/dsh-pet-live2d 固定提交 `053f0837284c4997634842e77906c083b060f9d1` 中的 `pets/ds-whale-girl/previews/idle.png`。

原角色上善无形，二次设计 ZipZipPipe，模型／绑定／表情氵六青。原美术许可为 CC BY-NC-SA 4.0。本头像及 PNG/ICO 导出沿用该许可，保留署名、非商业、相同方式共享条件；详情见根目录 NOTICE 和模型 LICENSE。

`app-avatar.png` 是通过内置图像编辑工具，以原头像为参照移除鼠标提示、补全右侧头发与头饰、增加透明留白后的头像改编。没有更换角色模型。`app-256.png` 与 `app.ico` 从该头像导出。

生成提示：

> Edit the provided original whale-girl avatar: remove the large blue computer mouse badge on the right and reconstruct the covered hair, face and maid headband. Preserve the same large blue eyes, periwinkle blue hair, side bows, white frilled maid headdress, rosy cheeks, smiling mouth, and tiny light-blue whale on her head. Fit the complete head in a square with a small transparent margin; only a hint of collar. Keep clear dark linework and flat cel shading, readable at small app icon sizes. Transparent background, no text, border, solid tile, watermark or added UI elements.

使用 `node scripts/build-branding.mjs`，通过本机 Edge 重新导出 16、24、32、48、64、128、256 像素图标并更新资源哈希。工具只做本机尺寸与格式转换，不调用图像生成服务。
