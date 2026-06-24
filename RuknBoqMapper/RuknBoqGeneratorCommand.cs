using System;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RuknBoqMapper
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RuknBoqManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!CommandHelper.EnsureActivated(commandData.Application))
                {
                    return Result.Cancelled;
                }

                RuknBoqApp.ShowWindow(commandData.Application, null);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportElementsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!CommandHelper.EnsureActivated(commandData.Application))
                {
                    return Result.Cancelled;
                }

                RuknBoqApp.ShowWindow(commandData.Application, "export");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ImportMappingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!CommandHelper.EnsureActivated(commandData.Application))
                {
                    return Result.Cancelled;
                }

                RuknBoqApp.ShowWindow(commandData.Application, "import");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Generate5DCodesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!CommandHelper.EnsureActivated(commandData.Application))
                {
                    return Result.Cancelled;
                }

                RuknBoqApp.ShowWindow(commandData.Application, "generate");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ValidateMappingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!CommandHelper.EnsureActivated(commandData.Application))
                {
                    return Result.Cancelled;
                }

                RuknBoqApp.ShowWindow(commandData.Application, "validate");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AuditReportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!CommandHelper.EnsureActivated(commandData.Application))
                {
                    return Result.Cancelled;
                }

                RuknBoqApp.ShowWindow(commandData.Application, "audit");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!CommandHelper.EnsureActivated(commandData.Application))
                {
                    return Result.Cancelled;
                }

                RuknBoqApp.ShowWindow(commandData.Application, "settings");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LicenseCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                RuknBoqApp.ShowLicenseWindow(commandData.Application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                RuknBoqApp.ShowLicenseWindow(commandData.Application, "Information");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    public static class CommandHelper
    {
        public static bool EnsureActivated(UIApplication uiApp)
        {
            if (!LicenseManager.IsActivated())
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\RuknTools\RuknBoqMapper"))
                {
                    if (key != null)
                    {
                        object expiresAtVal = key.GetValue("ExpiresAt");
                        if (expiresAtVal != null && DateTimeOffset.TryParse(expiresAtVal.ToString(), out DateTimeOffset expiresAt))
                        {
                            if (DateTimeOffset.UtcNow > expiresAt)
                            {
                                TaskDialog.Show("License Expired", "License Expired. Please contact the administrator to renew your subscription.");
                            }
                        }
                    }
                }
            }

            if (LicenseManager.IsActivated())
                return true;

            var win = new LicenseWindow();
            var helper = new System.Windows.Interop.WindowInteropHelper(win);
            helper.Owner = uiApp.MainWindowHandle;
            win.ShowDialog();

            return LicenseManager.IsActivated();
        }
    }
}
