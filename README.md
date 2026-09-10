# Bagua Live Wallpaper

基于用户提供的 MP4 视觉参考重新实现的 Windows 真正桌面层动态壁纸。

## 当前动态结构

- 黑色背景、古墨青细线与古金铜文字/符号。
- 八卦采用 GDI+ 矢量绘制，运行时实时绘制，不把 MP4 放大后当壁纸。
- 八卦盘比上一版明显放大。
- 每个同心环带独立旋转：相邻环带方向交替，速度相同。
- 中央阴阳保持固定。
- 默认缓慢连续旋转。
- 中文文字使用高质量抗锯齿实时绘制，保持清晰。
- 使用 Progman / WorkerW 桌面层承载窗口，桌面图标仍在上层，可以正常点击。
- 无 CMD 黑框、无任务栏按钮。
- 双击 BaguaLiveWallpaper.exe 即启动。
- Ctrl + Alt + Q 退出。
- **Ctrl + Alt + L 进入仿 Windows 动态锁屏；Enter 或 Esc 返回桌面。**
- 屏保模式仍通过 BaguaLiveWallpaper.scr 工作，进入屏保后按键或鼠标即可退出。
- 发布版为 Windows x64 self-contained single-file EXE，普通使用电脑不需要安装 Visual Studio、C++ 开发环境或另外安装 .NET Runtime。

## 构建

开发者机器需要 .NET 8 SDK：

```powershell
.\build-release.ps1
```

GitHub Actions 会自动构建 Windows x64 发布包。

<!-- uniform-lines-brighter-text desktop-only build -->
