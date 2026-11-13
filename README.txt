ColorCurver - Windows LUT image processor
v2.0.0
======================================

1. 直接使用（默认功能）

解压整个 ColorCurver 文件夹，双击 Main.bat，或者把一张或多张图片拖到 Main.bat 上。
在「Operation」选择 1，或直接回车，即为普通滤镜处理。
从列表中选择 Curves 里的 .cube 滤镜；直接回车使用 Default.txt 指定的默认滤镜。
未拖入图片时，程序会弹出文件选择窗口，可以一次选择多张。
处理后的 PNG 与输入图片位于同一文件夹；不会覆盖原图或已有输出。

预装的十个独立滤镜：
  ManuBrightness_450、500、550、600、650、700、750、800
  PrintShift_Forward     当前印制工序的平均色彩偏移模拟
  PrintShift_Inverse     根据上述偏移模型预计算的逆向补偿

默认滤镜是 ManuBrightness_600.cube。也可以在普通模式下直接选择
PrintShift_Forward.cube 或 PrintShift_Inverse.cube 单独输出处理后的图片。

2. 可选功能：逆向估计与正向回放

在「Operation」选择 2，进入逆向估计。
选择正向 LUT（当前预装 PrintShift_Forward），再选择你希望得到的最终画面。
程序使用与该正向 LUT 配对的预计算逆向 LUT，输出三份结果：
  *_VirtualOriginal.png    估计的输入图片（预补偿电子稿候选）
  *_ForwardReplay.png      将估计输入再次通过正向 LUT 的数字模拟画面
  *_Comparison.txt         回放画面与目标画面的 RGB 误差报告

例如：为模拟印制工序预补偿一张海报，输入希望印出来的目标图，
选择 PrintShift_Forward；VirtualOriginal 是可供试印的预补偿电子稿候选。
ForwardReplay 用于在电脑上与目标图对照。

重要：该模式调用已事先算好的 PrintShift_Inverse.cube，
不会在处理每张图片时重新数值求解正向 LUT 的反函数。
逆向结果是近似估计；被裁剪或不可达的颜色无法保证精确恢复。
报告只检验数字 LUT 闭环，并不代表真实印刷一定达到同等误差。

3. 增加或替换滤镜

所有滤镜都是单独的静态 3D .cube 文件，存放在 Curves 目录。
一份文件对应一种确定的色彩映射，不存在运行时调节强度/档位的参数。
例如要新增 ManuBrightness_650，只需添加独立文件；已预装该档。

Curves/Default.txt 只需填写一行希望默认使用的 LUT 文件名。
修改最外层程序文件夹的名称，无须改 Main.bat、Main.ps1 等内部文件。

如果需要让某份正向 LUT 在逆向模式下可用，应提供配套的离线逆向 LUT：
  Example_Forward.cube  <->  Example_Inverse.cube
也支持正向名为 Example.cube、逆向名为 Example_Inverse.cube。
逆向 LUT 推荐在文件开头包含用于绑定正向文件的注释：
  # SOURCE_FORWARD_SHA256 <正向文件的完整 SHA-256>
程序会检查该指纹是否匹配。没有指纹时可以使用，但会发出警告。
更换正向 LUT 后，应重新确认配套逆向 LUT 的正确性。

4. 文件结构

Main.bat         双击或拖拽入口
Main.ps1         文件/滤镜选择与运行流程
Library.cs       通用 .cube 解析、四面体插值、图片处理
Comparison.cs    仅在逆向回放模式下比较两张图片并生成简短报告
Curves/          独立 LUT 文件与 Default.txt

无须安装 Python、FFmpeg、Visual Studio 或第三方图像工具。
运行环境为 Windows 10/11、Windows PowerShell 5.1 和系统自带 .NET Framework。
支持 PNG、JPG/JPEG、BMP、TIFF、GIF；GIF 只处理第一帧。
输出保持输入图片宽高，保存为普通 8-bit PNG，保留透明度；不覆盖原文件。
当前只支持独立 3D .cube（不支持 1D 或 1D+3D shaper 组合文件）。
建议输入 SDR、sRGB 图片，并使用匹配输入色彩空间的 LUT。
不会自动做 Log/HDR/色域转换，也不保证保留原图片的 ICC、EXIF 元数据。

5. PrintShift 研究模型的适用范围

正向 PrintShift LUT 使用六组同一印制工序的海报原版与扫描实样拟合，
目标是模拟平均色偏。不同纸张、设备、参数和新的画面颜色可能产生不同误差。
六张样本的整图留一交叉验证，平均 RGB 误差约为 5.74/255。
该结果用于模型选择，不代表未来所有海报都能达到相同精度。
预计算逆向 LUT 与正向 LUT 的数字闭环良好，但这不是实际印刷质量验收。
