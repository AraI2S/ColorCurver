ColorCurver 2.0.1 - HDR JXR 直接升级补丁
=======================================

将本 ZIP 内的 3 个 .cs/.ps1 文件解压到现有 ColorCurver 根目录，并选择覆盖：
  Main.ps1
  Library.cs
  HDR.cs（新增）

保持 Main.bat / Comparison.cs / Curves / Default.txt / 所有 LUT 原样不动。
不要把这些文件放进 Curves 子目录。

升级后双击原来的 Main.bat，按回车使用默认滤镜，选择 JXR 文件。
JXR HDR 自动无损输出同目录的 .jxr；其它文件仍输出普通 PNG。
逆向预估仍只支持 SDR，不适用于 JXR。

完整使用说明请参考完整版本 ZIP 内的 README.txt。
本补丁需要 Windows 10/11、Windows PowerShell 5.1、系统内置 WIC/WPF。
