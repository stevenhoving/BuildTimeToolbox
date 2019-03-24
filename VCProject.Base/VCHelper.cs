using EnvDTE;
using Microsoft.VisualStudio.VCProjectEngine;

#if VC15
namespace VCProjectUtils.VS15
#endif
{
    public class VCHelper : VCProjectUtils.Base.IVCHelper
    {
        public bool IsVCProject(Project project)
        {
            return project?.Object is VCProject;
        }

        private VCFileConfiguration GetVCFileConfigForCompilation(Document document, out string reasonForFailure)
        {
            if (document == null)
            {
                reasonForFailure = "No document.";
                return null;
            }

            var vcProject = document.ProjectItem.ContainingProject?.Object as VCProject;
            if (vcProject == null)
            {
                reasonForFailure = "The given document does not belong to a VC++ Project.";
                return null;
            }

            VCFile vcFile = document.ProjectItem?.Object as VCFile;
            if (vcFile == null)
            {
                reasonForFailure = "The given document is not a VC++ file.";
                return null;
            }

            if (vcFile.FileType != eFileType.eFileTypeCppCode)
            {
                reasonForFailure = "The given document is not a compileable VC++ file.";
                return null;
            }

            IVCCollection fileConfigCollection = vcFile.FileConfigurations as IVCCollection;
            VCFileConfiguration fileConfig = fileConfigCollection?.Item(vcProject.ActiveConfiguration.Name) as VCFileConfiguration;
            if (fileConfig == null)
            {
                reasonForFailure = "Failed to retrieve file config from document.";
                return null;
            }

            reasonForFailure = "";
            return fileConfig;
        }

        public static VCCLCompilerTool GetCompilerTool(Project project, out string reasonForFailure)
        {
            VCProject vcProject = project?.Object as VCProject;
            if (vcProject == null)
            {
                reasonForFailure = "Failed to retrieve VCCLCompilerTool since project is not a VCProject.";
                return null;
            }
            VCConfiguration activeConfiguration = vcProject.ActiveConfiguration;
            var tools = activeConfiguration.Tools;
            VCCLCompilerTool compilerTool = null;
            foreach (var tool in activeConfiguration.Tools)
            {
                compilerTool = tool as VCCLCompilerTool;
                if (compilerTool != null)
                    break;
            }

            if (compilerTool == null)
            {
                reasonForFailure = "Couldn't file a VCCLCompilerTool in VC++ Project.";
                return null;
            }

            reasonForFailure = "";
            return compilerTool;
        }

        public bool IsCompilableFile(Document document, out string reasonForFailure)
        {
            return GetVCFileConfigForCompilation(document, out reasonForFailure) != null;
        }

        public void CompileSingleFile(Document document)
        {
            string reasonForFailure;
            var fileConfig = GetVCFileConfigForCompilation(document, out reasonForFailure);
            if(fileConfig != null)
            {
                fileConfig.Compile(true, false); // WaitOnBuild==true always fails.
            }
        }

        public string GetCompilerSetting_Includes(Project project, out string reasonForFailure)
        {
            VCCLCompilerTool compilerTool = GetCompilerTool(project, out reasonForFailure);
            return compilerTool?.FullIncludePath;
        }

        public void SetCompilerSetting_ShowIncludes(Project project, bool show, out string reasonForFailure)
        {
            VCCLCompilerTool compilerTool = GetCompilerTool(project, out reasonForFailure);
            if (compilerTool != null)
                //compilerTool.ShowIncludes = show;
                compilerTool.AdditionalOptions = "-d1reportTime";
        }

        public bool? GetCompilerSetting_ShowIncludes(Project project, out string reasonForFailure)
        {
            VCCLCompilerTool compilerTool = GetCompilerTool(project, out reasonForFailure);
            return compilerTool.AdditionalOptions.Contains("-d1reportTime");
            //return compilerTool?.ShowIncludes;
        }

        public string GetCompilerSetting_PreprocessorDefinitions(Project project, out string reasonForFailure)
        {
            VCCLCompilerTool compilerTool = GetCompilerTool(project, out reasonForFailure);
            return compilerTool?.PreprocessorDefinitions;
        }
    }
}
