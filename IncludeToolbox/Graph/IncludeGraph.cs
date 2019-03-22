using Microsoft.VisualStudio.PlatformUI;
using System.Collections.Generic;

namespace IncludeToolbox.Graph
{
    /// <summary>
    /// Graph of files including files.
    /// </summary>
    public class IncludeGraph
    {
        public struct Include
        {
            /// <summary>
            /// Absolute path to the file that includes another file.
            /// </summary>
            /// <remarks>May be null, signaling that the include line could not be resolved.</remarks>
            public GraphItem IncludedFile;

            /// <summary>
            /// The original include line in sourceFile.
            /// </summary>
            /// <remarks>Depending on the graph generation algorithm, this may be null.</remarks>
            public Formatter.IncludeLineInfo IncludeLine;
        }

        public class GraphItem
        {
            public GraphItem(string absoluteFilename, double time)
            {
                AbsoluteFilename = absoluteFilename;
                FormattedName = absoluteFilename;
                Time = time;
                Includes = new List<Include>();
            }

            /// <summary>
            /// Absolute path to the file that includes the other files.
            /// </summary>
            /// <remarks>
            /// If an absolute filename can't be provided (e.g. due to resolve failure), this can be any kind of unique file identifier.
            /// This is also used as key in the graph item dictionary.
            /// </remarks>
            public string AbsoluteFilename { get; private set; }

            /// <summary>
            /// A formatted name that can be set from the outside. Is by default the same as AbsoluteFilename.
            /// </summary>
            public string FormattedName { get; set; }

            public double Time { get; set; }

            /// <summary>
            /// List of all includes of this file.
            /// </summary>
            public List<Include> Includes { get; private set; }
        }


        public IReadOnlyCollection<GraphItem> GraphItems => graphItems.Values;

        /// <summary>
        /// Map of all files that the graph reaches.
        /// Use CreateOrAddItem to populate it.
        /// </summary>
        private Dictionary<string, GraphItem> graphItems = new Dictionary<string, GraphItem>();

        /// <summary>
        /// Retrieves item from a given identifying absolute filename.
        /// </summary>
        /// <param name="filename">
        /// Filename of an include, may be relative. Will be normalized internally.
        /// If an absolute filename can't be provided (e.g. due to resolve failure), this can be any kind of unique file identifier.
        /// </param>
        public GraphItem CreateOrGetItem(string filename, double time, out bool isNew)
        {
            filename = Utils.GetExactPathName(filename);
            return CreateOrGetItem_AbsoluteNormalizedPath(filename, time, out isNew);
        }

        public GraphItem CreateOrGetItem_AbsoluteNormalizedPath(string normalizedAbsoluteFilename, double time, out bool isNew)
        {
            GraphItem outItem;
            isNew = !graphItems.TryGetValue(normalizedAbsoluteFilename, out outItem);
            if (isNew)
            {
                outItem = new GraphItem(normalizedAbsoluteFilename, time);
                graphItems.Add(normalizedAbsoluteFilename, outItem);
            }
            return outItem;
        }

        public DGMLGraph ToDGMLGraph()
        {
            DGMLGraph dgmlGraph = new DGMLGraph();
            foreach (GraphItem node in graphItems.Values)
            {
                dgmlGraph.Nodes.Add(new DGMLGraph.Node { Id = node.AbsoluteFilename, Label = node.FormattedName });
                foreach (Include include in node.Includes)
                {
                    dgmlGraph.Links.Add(new DGMLGraph.Link { Source = node.AbsoluteFilename, Target = include.IncludedFile?.AbsoluteFilename ?? null });
                }
            }

            return dgmlGraph;
        }
    }
}
