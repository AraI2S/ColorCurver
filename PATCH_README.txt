ColorCurver v2.0.3 - System.Xaml C# compilation fix
===================================================

Fixes error:
  HDR.cs(181): IUriContext is defined in assembly System.Xaml (CS0012)

On your existing v2.0.2 ColorCurver installation:
  1. Close ColorCurver.
  2. Extract Main.ps1 from this ZIP to your ColorCurver top-level folder.
  3. Overwrite the old Main.ps1 (and only that file).
  4. Relaunch Main.bat and test the same JXR image.

No LUTs, custom Qiaoxin curves, or other application files are changed.
This is a compilation dependency repair, not a change in the HDR image algorithm.
The C# reference set now includes absolute assembly paths for:
  System.Drawing, PresentationCore, WindowsBase, System.Xaml.
The script retrieves paths from the already loaded .NET Framework assemblies.

If a new issue appears, provide the COMPLETE error message; errors after
successful compilation may originate from WIC/JXR decode or encode stages.

Current version: 2.0.3 (patch iteration; final Windows acceptance pending).
