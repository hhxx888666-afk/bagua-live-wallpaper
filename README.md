# Bagua Live Wallpaper

基于用户提供的 MP4 视觉参考重新实现的 Windows 真正桌面层动态壁纸。

## 当前动态结构

- 黑色背景、白色八卦/阴阳图。
- 八卦采用 GDI+ 矢量绘制，运行时实时绘制，不把 MP4 放大后当壁纸。
- 八卦盘比上一版明显放大。
- **四个同心动态层独立旋转：第1圈顺时针 → 第2圈逆时针 → 第3圈顺时针 → 第4圈逆时针。**
- 每一层的圆环、分格、文字和符号作为一个整体旋转，不是整张图一起转。
- 默认缓慢连续旋转：约 24 秒一整圈。
- 中文文字使用高质量抗锯齿实时绘制，保持清晰。
- 使用 Progman / WorkerW 桌面层承载窗口，桌面图标仍在上层，可以正常点击。
- 无 CMD 黑框、无任务栏按钮。
- 双击 BaguaLiveWallpaper.exe 即启动。
- Ctrl + Alt + Q 退出。
- 发布版为 Windows x64 self-contained single-file EXE，普通使用电脑不需要安装 Visual Studio、C++ 开发环境或另外安装 .NET Runtime。

## 构建

开发者机器需要 .NET 8 SDK：

```powershell
.\build-release.ps1
```

GitHub Actions 会自动构建 Windows x64 发布包。
