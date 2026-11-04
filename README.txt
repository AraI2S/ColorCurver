ColorCurver — 3D LUT 图片处理工具
================================

一、使用

1. 将整个 ColorCurver 文件夹解压到 Windows 电脑上。
2. 双击 Main.bat，或把一张/多张图片拖放到 Main.bat 上。
3. 若 Curves 中有多份滤镜，输入序号选择；直接回车采用默认滤镜。
4. 没有拖入图片时，使用文件选择窗口选择一张或多张图片。
5. 程序将直接在原图所在目录生成新 PNG，不会覆盖原图或已有成果。

每份 .cube 是固定滤镜，选择后直接应用；无强度、亮度等级等参数输入。
一次可以处理多张图片，只选择一份滤镜。

示例：
  原图：Movie.png
  应用目录：ColorCurver
  选中的滤镜：ManuBrightness600.cube
  输出：Movie_ColorCurver_ManuBrightness600.png

如果目标文件存在，自动依次使用 _2、_3 等后缀，防止覆盖。

二、文件结构

ColorCurver/                     顶层文件夹可以随时改名
  Main.bat                       Windows 启动器
  Main.ps1                       通用程序、文件选择与管理
  Library.cs                     .cube 解析、3D 插值和图片处理引擎
  Curves/
    ManuBrightness600.cube       默认静态 3D LUT
    Default.txt                  默认滤镜文件名（可选）
  README.txt                     使用说明

修改顶层文件夹名后，程序窗口标题、输出图文件名中的程序名会自动跟随。
内部文件名、内部相对引用及滤镜内容无需修改。

三、添加、删除、选择滤镜

把标准的 3D .cube 文件放进 Curves 文件夹即可。
程序只读取 .cube；以前的 .json 文件已不支持，可删除。
每份 .cube 是一个独立的、无需运行时参数的固定效果。
例如 ManuBrightness500.cube 和 ManuBrightness700.cube 要分别制作。
添加、删除或替换滤镜无需修改任何程序文件。

Curves/Default.txt 里仅写一个 .cube 文件名，比如：
  ManuBrightness600.cube

默认文件名必须是 Curves 文件夹中的实际文件名。
若不存在 Default.txt，按文件名排序选第一份作为默认；
若目录中只有一份 .cube，则直接使用，省略选择界面。
选择多份滤镜时，回车采用默认滤镜。

四、接受的 3D .cube 规范

- UTF-8 或普通 ASCII 文本，文件扩展名为 .cube（大小写均可）。
- 必须包含 LUT_3D_SIZE N，N 从 2 到 256，恰有 N×N×N 行 RGB 数据。
- 每行依次为输出红、绿、蓝三个有限浮点数；典型值 0–1。
- 数据顺序：红轴变化最快、绿轴其次、蓝轴最慢。
- 可包含 TITLE "..."，可包含 # 注释或空行。
- 可包含 DOMAIN_MIN r g b 与 DOMAIN_MAX r g b；默认各为 0 与 1。
- 兼容部分软件的 LUT_3D_INPUT_RANGE min max（共用输入范围）。
- 输入坐标越界时夹到对应边界；输出被裁到 0–1，并转换成 8-bit。
- 使用四面体插值，对混合 RGB 通道的 3D LUT 同样有效。
- 为避免歧义，DOMAIN_* 与 LUT_3D_INPUT_RANGE 不应同时定义。

当前版本只接受独立 3D LUT：不接受 1D LUT、1D shaper+3D 组合文件、
Hald PNG、.3dl、旧版 .json 或额外的厂商私有头。
通常 17、33、65 级 LUT 都可用；较大的 .cube 需要更多内存与加载时间。

注意：.cube 只定义 RGB 数值映射，本身不会自动转换图像色域、
传递函数（例如 Log 转 sRGB）或切换 HDR/SDR 工作空间。
应先确认图片与滤镜的输入色彩空间一致，建议使用普通 SDR sRGB 图片。

五、图像处理与限制

系统：Windows 10/11，自带 Windows PowerShell 5.1 与 .NET Framework。
输入：PNG、JPG/JPEG、BMP、TIFF、GIF（GIF 只处理首帧）。
输出：原分辨率 8-bit PNG。保留图像宽高及透明度，不做 AI 重绘或缩放。
不保证保留原图的 ICC、EXIF 等元数据，也不保证 16-bit / HDR 精度。
程序不会改变屏幕 Gamma Ramp 或系统显示亮度。
工作目录与滤镜目录均通过脚本相对路径定位，不依赖安装目录名称。

第一次安装建议解压到全新目录。升级时可备份所需的 .cube，
再复制到新版 Curves 文件夹。旧 .json 不会被读取。
