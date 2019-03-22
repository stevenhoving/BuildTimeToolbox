using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IncludeToolbox.Graph
{
    public static class CompilationBasedGraphParser
    {
        public delegate void OnCompleteCallback(IncludeGraph graph, bool success);

        // There can always be only one compilation operation and it takes a while.
        // This makes the whole mechanism effectively a singletonish thing.
        private static bool CompilationOngoing { get { return documentBeingCompiled != null; } }
        private static bool showIncludeSettingBefore = false;
        private static OnCompleteCallback onCompleted;
        private static Document documentBeingCompiled;
        private static IncludeGraph graphBeingExtended;


        public static bool CanPerformShowIncludeCompilation(Document document, out string reasonForFailure)
        {
            if (CompilationOngoing)
            {
                reasonForFailure = "Can't compile while another file is being compiled.";
                return false;
            }

            var dte = VSUtils.GetDTE();
            if (dte == null)
            {
                reasonForFailure = "Failed to acquire dte object.";
                return false;
            }

            if (VSUtils.VCUtils.IsCompilableFile(document, out reasonForFailure) == false)
            {
                reasonForFailure = string.Format("Can't extract include graph since current file '{0}' can't be compiled: {1}.", document?.FullName ?? "<no file>", reasonForFailure);
                return false;
            }

            return true;
         }

        /// <summary>
        /// Parses a given source file using cl.exe with the /showIncludes option and adds the output to the original graph.
        /// </summary>
        /// <remarks>
        /// If this is the first file, the graph is necessarily a tree after this operation.
        /// </remarks>
        /// <returns>true if successful, false otherwise.</returns>
        public static bool AddIncludesRecursively_ShowIncludesCompilation(this IncludeGraph graph, Document document, OnCompleteCallback onCompleted)
        {
            if (!CanPerformShowIncludeCompilation(document, out string reasonForFailure))
            {
                Output.Instance.ErrorMsg(reasonForFailure);
                return false;
            }

            try
            {
                var dte = VSUtils.GetDTE();
                if (dte == null)
                {
                    Output.Instance.ErrorMsg("Failed to acquire dte object.");
                    return false;
                }

                {
                    bool? setting = VSUtils.VCUtils.GetCompilerSetting_ShowIncludes(document.ProjectItem?.ContainingProject, out reasonForFailure);
                    if (!setting.HasValue)
                    {
                        Output.Instance.ErrorMsg("Can't compile with show includes: {0}.", reasonForFailure);
                        return false;
                    }
                    else
                        showIncludeSettingBefore = setting.Value;

                    VSUtils.VCUtils.SetCompilerSetting_ShowIncludes(document.ProjectItem?.ContainingProject, true, out reasonForFailure);
                    if (!string.IsNullOrEmpty(reasonForFailure))
                    {
                        Output.Instance.ErrorMsg("Can't compile with show includes: {0}.", reasonForFailure);
                        return false;
                    }
                }

                // Only after we're through all early out error cases, set static compilation infos.
                dte.Events.BuildEvents.OnBuildDone += OnBuildConfigFinished;
                CompilationBasedGraphParser.onCompleted = onCompleted;
                CompilationBasedGraphParser.documentBeingCompiled = document;
                CompilationBasedGraphParser.graphBeingExtended = graph;

                // Even with having the config changed and having compile force==true, we still need to make a dummy change in order to enforce recompilation of this file.
                {
                    document.Activate();
                    var documentTextView = VSUtils.GetCurrentTextViewHost();
                    var textBuffer = documentTextView.TextView.TextBuffer;
                    using (var edit = textBuffer.CreateEdit())
                    {
                        edit.Insert(0, " ");
                        edit.Apply();
                    }
                    using (var edit = textBuffer.CreateEdit())
                    {
                        edit.Replace(new Microsoft.VisualStudio.Text.Span(0, 1), "");
                        edit.Apply();
                    }
                }

                VSUtils.VCUtils.CompileSingleFile(document);
            }
            catch(Exception e)
            {
                ResetPendingCompilationInfo();
                Output.Instance.ErrorMsg("Compilation of file '{0}' with /showIncludes failed: {1}.", document.FullName, e);
                return false;
            }

            return true;
        }

        private static void ResetPendingCompilationInfo()
        {
            string reasonForFailure;
            VSUtils.VCUtils.SetCompilerSetting_ShowIncludes(documentBeingCompiled.ProjectItem?.ContainingProject, showIncludeSettingBefore, out reasonForFailure);

            onCompleted = null;
            documentBeingCompiled = null;
            graphBeingExtended = null;

            VSUtils.GetDTE().Events.BuildEvents.OnBuildDone -= OnBuildConfigFinished;
        }

        private static int ParseInt(string text, string token)
        {
            var parts = text.Split(new string[] { token }, StringSplitOptions.None);
            return Convert.ToInt32(parts[1]);
        }

        private static string CleanString(string text)
        {
            return text.Split(new string[] { ">\t" }, StringSplitOptions.None)[1];
        }

        struct FilenameAndTime
        {
            public string filename;
            public double time; // time in seconds
            public int count; // header count
        };

        private static FilenameAndTime GetFilenameAndTime(string text)
        {
            var parts = text.Split(new string[] { ":" }, StringSplitOptions.None);
            var result = new FilenameAndTime();
            var time_string = parts.Last();

            time_string = time_string.Substring(0, time_string.Count() - 1); // remove the last letter
            result.time = Convert.ToDouble(time_string);
            result.filename = parts[0].Split('\t').Last() + ':' + parts[1];

            return result;
        }

        private static void OnBuildConfigFinished(vsBuildScope Scope, vsBuildAction Action)
        {
            // Sometimes we get this message several times.
            if (!CompilationOngoing)
                return;

            // Parsing maybe successful for an unsuccessful build!
            bool successfulParsing = true;
            try
            {
                string outputText = VSUtils.GetOutputText();
                if (string.IsNullOrEmpty(outputText))
                {
                    successfulParsing = false;
                    return;
                }

                // What we're building right now is a tree.
                // However, combined with the existing data it might be a wide graph.
                var includeTreeItemStack = new Stack<IncludeGraph.GraphItem>();
                includeTreeItemStack.Push(graphBeingExtended.CreateOrGetItem(documentBeingCompiled.FullName, 0.0, out _));

                var includeDirectories = VSUtils.GetProjectIncludeDirectories(documentBeingCompiled.ProjectItem.ContainingProject);
                includeDirectories.Insert(0, PathUtil.Normalize(documentBeingCompiled.Path) + Path.DirectorySeparatorChar);

                //const string includeNoteString = "Note: including file: ";
                const string includeHeadersString = "Include Headers:";
                string[] outputLines = System.Text.RegularExpressions.Regex.Split(outputText, "\r\n|\r|\n"); // yes there are actually \r\n in there in some VS versions.

                /*
                 mode:
                 - 0 means not in a parsing mode
                 - 1 means parsing 'Include Headers:' section
                 */
                int mode = 0;

                for (int line_index = 0; line_index < outputLines.Count(); line_index++)
                {
                    string line = outputLines[line_index];
                    // keeping track parsing state
                    if (mode == 0)
                    {
                        if (line.IndexOf(includeHeadersString) >= 0)
                        {
                            mode = 1;
                            continue;
                        }
                        // hack we do not support other states yet
                        continue;
                    }

                    line = CleanString(line);
                    int count = ParseInt(line, "Count:");
                    for (int include_line_index = 0; include_line_index < count; include_line_index++)
                    {
                        line_index++;
                        string include_line = outputLines[line_index];
                        include_line = CleanString(include_line);
                        int depth = include_line.Count(f => f == '\t') - 1;

                        if (depth >= includeTreeItemStack.Count)
                        {
                            includeTreeItemStack.Push(includeTreeItemStack.Peek().Includes.Last().IncludedFile);
                        }
                        while (depth < includeTreeItemStack.Count - 1)
                            includeTreeItemStack.Pop();

                        var filename_and_time = GetFilenameAndTime(include_line);
                        IncludeGraph.GraphItem includedItem = graphBeingExtended.CreateOrGetItem(filename_and_time.filename, filename_and_time.time, out _);
                        includeTreeItemStack.Peek().Includes.Add(new IncludeGraph.Include() { IncludedFile = includedItem });
                    }
                    mode = 0;

/*
                    int startIndex = 0;
                    startIndex += includeHeadersString.Length;

                    int includeStartIndex = startIndex;
                    while (includeStartIndex < line.Length && line[includeStartIndex] == ' ')
                        ++includeStartIndex;
                    int depth = includeStartIndex - startIndex;

                    if (depth >= includeTreeItemStack.Count)
                    {
                        includeTreeItemStack.Push(includeTreeItemStack.Peek().Includes.Last().IncludedFile);
                    }
                    while (depth < includeTreeItemStack.Count - 1)
                        includeTreeItemStack.Pop();

                    string fullIncludePath = line.Substring(includeStartIndex);
                    IncludeGraph.GraphItem includedItem = graphBeingExtended.CreateOrGetItem(fullIncludePath, out _);
                    includeTreeItemStack.Peek().Includes.Add(new IncludeGraph.Include() { IncludedFile = includedItem });
*/
                }
            }

            catch(Exception e)
            {
                Output.Instance.ErrorMsg("Failed to parse output from /showInclude compilation of file '{0}': {1}", documentBeingCompiled.FullName, e);
                successfulParsing = false;
                return;
            }
            finally
            {
                try
                {
                    onCompleted(graphBeingExtended, successfulParsing);
                }
                finally
                {
                    ResetPendingCompilationInfo();
                }
            }
        }
    }
}
