using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace IncludeToolbox
{
    [Guid("769AFCC2-25E2-459A-B2A3-89D7308800BD")]
    public class ViewerOptionsPage : DialogPage
    {
        public const string SubCategory = "Include Viewer & Graph";
        private const string collectionName = "IncludeViewer";

        [Category("BuildTime Graph Parsing")]
        [DisplayName("Graph Endpoint Directories")]
        [Description("List of absolute directory paths. For any include below these paths, the graph parsing will stop.")]
        public string[] NoParsePaths
        {
            get { return noParsePaths; }
            set
            {
                // It is critical that the paths are "exact" since we want to use them as with string comparison.
                noParsePaths = value;
                for (int i = 0; i < noParsePaths.Length; ++i)
                    noParsePaths[i] = Utils.GetExactPathName(noParsePaths[i]);
            }
        }
        private string[] noParsePaths;

        private WritableSettingsStore GetSettingsStore()
        {
            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            return settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        public override void SaveSettingsToStorage()
        {
            var settingsStore = GetSettingsStore();

            if (!settingsStore.CollectionExists(collectionName))
                settingsStore.CreateCollection(collectionName);

            var value = string.Join("\n", NoParsePaths);
            settingsStore.SetString(collectionName, nameof(NoParsePaths), value);
        }

        public override void LoadSettingsFromStorage()
        {
            var settingsStore = GetSettingsStore();

            if (settingsStore.PropertyExists(collectionName, nameof(NoParsePaths)))
            {
                var value = settingsStore.GetString(collectionName, nameof(NoParsePaths));
                NoParsePaths = value.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                // It is surprisingly hard to get to the standard library paths.
                // Even finding the VS install path is not easy and - as it turns out - not necessarily where the standard library files reside.
                // So in lack of a better idea, we just put everything under the program files folder in here.
                string programFiles = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
                string programFilesX86 = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%");
                if(programFiles == programFilesX86) // If somebody uses still x86 system.
                    NoParsePaths = new string[] { programFiles  };
                else
                    NoParsePaths = new string[] { programFiles, programFilesX86 };
            }
        }
    }
}
