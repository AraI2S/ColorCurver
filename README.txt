本地图像色调映射工具 — 使用说明
=================================
版本：1.1（Windows PowerShell 5.1）

【改名规则：只改最外层文件夹】
当前文件夹的名称，就是这款程序显示和输出使用的名称。
例如把整个文件夹命名为 ExampleA，程序就显示 ExampleA；
将来改名为任意其他名称，也只需直接重命名这个文件夹。

内部文件名 Main.bat、Main.ps1、Library.cs 永远不用改。
这些文件的相互引用只使用固定文件名和脚本所在目录，
没有写死应用品牌名，也不依赖安装路径或当前工作目录。

【文件结构】
你的应用名称（可自由修改）/
  Main.bat       双击入口 / 可拖拽图片的入口
  Main.ps1       图片选择、参数输入、批量处理逻辑
  Library.cs     核心像素处理公式及 PNG 输出
  README.txt     本说明
  Test/
    Original.png    测试输入图
    Expected_600.png  600 档的预期输出图

【日常使用】
1. 解压 ZIP，将整个文件夹放到你喜欢的位置。
2. 双击 Main.bat；或者把一张/多张图片拖到 Main.bat。
3. 输入 0~1000 的整数数值（直接回车默认 600），按回车。
4. 如未拖入图片，程序会弹出图片选择窗口，可一次选择多张。
5. 在原图同目录生成新的 PNG，不覆盖原图。

若文件夹命名为 ExampleA：
  Movie.png + 数值 600 → Movie_ExampleA600.png
若文件夹后来更名为 NewName：
  Movie.png + 数值 600 → Movie_NewName600.png
输出命名会自动随文件夹名称更新，无需修改任何代码。
若同名输出已存在，自动使用 _2、_3 等后缀。

【算法】
保留 ManuBrightness V5/MapVersion 5 的 0~1000 完整分段映射：
  0~200：Hermite 曲线，初始斜率 2.5
  200~800：线性映射，200→350、800→840
  800~1000：高端指数曲线，power=2.0
每个 RGB 通道执行：
  dark = max(0, (500-effectiveLevel)/500)
  bright = max(0, (effectiveLevel-500)/500)
  gamma = 1 + 1.20*dark - 0.50*bright
  gain = 1 - 0.85*dark + 0.25*bright
  lift = 0.08*bright
  y = clamp((x^gamma)*gain + lift, 0, 1)
再四舍六入五成双到 8-bit（.NET Math.Round ToEven）。
600 档时：effective=676.666666667、gamma=0.823333333、
          gain=1.088333333、lift=0.028266667。

【首次校验】
对 Test/Original.png 使用 600 档。
将生成的 Original_<当前文件夹名>600.png 与
Test/Expected_600.png 作比较，正常应具有相同的 RGB 像素。

【格式与边界】
- Windows 10 / 11，Windows PowerShell 5.1，.NET System.Drawing。
- 不需要安装 Python、XnConvert、ImageMagick。
- 推荐 8-bit SDR/sRGB PNG、JPG；还可读 BMP、TIFF、GIF。
- 保存为新的 8-bit PNG，无缩放、无重绘，保留分辨率。
- 透明通道会保留；不保证保留原始 ICC、EXIF 和其它元数据。
- 广色域、HDR、16-bit 图像建议先转为 8-bit SDR/sRGB。
- 复现的是 ManuBrightness 数学像素映射；受显示系统色彩管线
  影响，处理后的图片与原图配合屏幕 Gamma Ramp 的观感未必完全一致。
- Main.bat 仅对本次 PowerShell 进程使用 ExecutionPolicy Bypass，
  不永久修改系统执行策略；组织策略仍可能阻止运行。
- 请勿重命名内部文件，重命名最外层文件夹即可。

【实施说明】
本包已核对文件引用、无品牌硬编码及算法测试图；
当前环境无法执行 Windows PowerShell 5.1，因此首次请先测试校验图。
