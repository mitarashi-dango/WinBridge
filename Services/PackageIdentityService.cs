using System.Runtime.InteropServices;
using System.Text;

namespace WinBridge.Services;

internal static class PackageIdentityService
{
    internal const int AppModelErrorNoPackage = 15700;

    public static bool IsPackaged
    {
        get
        {
            try
            {
                uint packageFullNameLength = 0;
                var result = GetCurrentPackageFullName(ref packageFullNameLength, null);
                return IsPackagedResult(result);
            }
            catch (DllNotFoundException)
            {
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                return true;
            }
        }
    }

    internal static bool IsPackagedResult(int errorCode) =>
        errorCode != AppModelErrorNoPackage;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength,
        StringBuilder? packageFullName);
}
