# Bagua Live Wallpaper

基于用户提供的 `bagua_concentric_alternating_1920x1080(2).mp4` 视觉参考，重新从零实现的 Windows 真正桌面层动态壁纸。

## 已实现目标

- 黑色背景、白色八卦/阴阳图。
- 八卦采用 **GDI+ 矢量绘制**，运行时实时绘制，不把 MP4 放大后当壁纸，避免视频缩放导致的模糊。
- 多层同心圆、卦象、六十四卦文字环，按本次提供的视频构图进行重绘。
- 默认缓慢连续旋转：约 **24 秒一整圈**。
- 使用 `Progman / WorkerW` 桌面层承载窗口，桌面图标仍在上层，可以正常点击和打开文件。
- 无边框、无任务栏按钮、无 CMD 黑框。
- 双击 `BaguaLiveWallpaper.exe` 即启动。
- 程序不会替换 Windows 原壁纸；直接绘制在桌面壁纸层，因此退出后原壁纸天然保持不变。
- `Ctrl + Alt + Q` 退出。
- 单实例保护，重复双击不会启动多个壁纸。
- 发布版为 Windows x64 self-contained single-file EXE，普通使用电脑不需要安装 Visual Studio、C++ 开发环境或另外安装 .NET Runtime。

## 构建

开发者机器需要 .NET 8 SDK。运行：

```powershell
.\build-release.ps1
```

输出：

```text
publish\BaguaLiveWallpaper.exe
```

GitHub Actions 会自动构建 Windows x64 发布包。

## 退出

按 `Ctrl + Alt + Q`。因为程序从未修改系统壁纸，退出后 Windows 原壁纸直接保持原状。

## 设计原则

本项目刻意没有使用视频播放器。视频只作为视觉参考，运行时实时绘制图形并旋转，确保八卦图形清晰锐利，同时保留桌面文件可见、可操作的真正桌面壁纸体验。
