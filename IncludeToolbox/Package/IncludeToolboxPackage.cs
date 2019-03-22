using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace IncludeToolbox
{
    [ProvideBindingPath(SubPath = "")]   // Necessary to find packaged assemblies.

    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [ProvideMenuResource("Menus.ctmenu", 1)]

    [ProvideOptionPage(typeof(FormatterOptionsPage), Options.Constants.Category, FormatterOptionsPage.SubCategory, 1000, 1001, true)]
    [ProvideOptionPage(typeof(TryAndErrorRemovalOptionsPage), Options.Constants.Category, TryAndErrorRemovalOptionsPage.SubCategory, 1000, 1003, true)]
    [ProvideOptionPage(typeof(ViewerOptionsPage), Options.Constants.Category, ViewerOptionsPage.SubCategory, 1000, 1004, true)]

    [ProvideToolWindow(typeof(GraphWindow.IncludeGraphToolWindow))]
    [Guid(IncludeToolboxPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [InstalledProductRegistration("#110", "#112", "0.2", IconResourceID = 400)]
    public sealed class IncludeToolboxPackage : Package
    {
        /// <summary>
        /// IncludeToolboxPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "5c2743c4-1b3f-4edd-b6a0-4379f867d47f";

        static public Package Instance { get; private set; }

        public IncludeToolboxPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            Instance = this;
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Commands.IncludeGraphToolWindow.Initialize(this);
            Commands.FormatIncludes.Initialize(this);
            Commands.TryAndErrorRemoval_CodeWindow.Initialize(this);
            Commands.TryAndErrorRemoval_Project.Initialize(this);

            base.Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        #endregion
    }
}
