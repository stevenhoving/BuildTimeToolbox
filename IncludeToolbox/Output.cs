﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace IncludeToolbox
{
    public class Output
    {
        static public Output Instance { private set; get; } = new Output();

        public const int MessageBoxResult_Yes = 6;

        private Output()
        {
        }

        private OutputWindowPane outputWindowPane = null;

        public void Init()
        {
            DTE2 dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as DTE2;
            if (dte == null)
                return;

            OutputWindow outputWindow = dte.ToolWindows.OutputWindow;
            outputWindowPane = outputWindow.OutputWindowPanes.Add("BuildTimeToolbox");
        }

        public void Clear()
        {
            if (outputWindowPane == null)
            {
                Init();
            }
            outputWindowPane.Clear();
        }

        public void Write(string text)
        {
            if (outputWindowPane == null)
            {
                Init();
            }
            if (outputWindowPane != null)
            {
                System.Diagnostics.Debug.Assert(outputWindowPane != null);
                outputWindowPane.OutputString(text);
            }
        }

        public void Write(string text, params object[] stringParams)
        {
            string output = string.Format(text, stringParams);
            Write(output);
        }

        public void WriteLine(string line)
        {
            Write(line + '\n');
        }

        public void WriteLine(string line, params object[] stringParams)
        {
            string output = string.Format(line, stringParams);
            WriteLine(output);
        }

        public void ErrorMsg(string message, params object[] stringParams)
        {
            string output = string.Format(message, stringParams);
            WriteLine(output);
            VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider, output, "BuildTime Toolbox", OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public void InfoMsg(string message, params object[] stringParams)
        {
            string output = string.Format(message, stringParams);
            WriteLine(output);
            VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider, output, "BuildTime Toolbox", OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public void OutputToForeground()
        {
            outputWindowPane.Activate();
        }
    }
}
