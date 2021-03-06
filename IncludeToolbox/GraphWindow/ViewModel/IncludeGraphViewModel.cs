﻿using IncludeToolbox.Formatter;
using IncludeToolbox.Graph;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace IncludeToolbox.GraphWindow
{
    // Creates a CollectionView for databinding to a HierarchicalTemplate ItemSource
    // \see https://social.msdn.microsoft.com/Forums/vstudio/en-US/0fe5045e-9753-4d7f-ba28-7c23ed213dd6/treeview-sort?forum=wpf
    public class CollectionViewFactoryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Collections.IList collection = value as System.Collections.IList;
            ListCollectionView view = new ListCollectionView(collection);
            SortDescription sort = new SortDescription(parameter.ToString(), ListSortDirection.Descending);
            view.SortDescriptions.Add(sort);
            return view;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    };

    public class IncludeGraphViewModel : PropertyChangedBase
    {
        public HierarchyIncludeTreeViewItem HierarchyIncludeTreeModel { get; set; } = new HierarchyIncludeTreeViewItem(new IncludeGraph.Include(), "");
        public FolderIncludeTreeViewItem_Root FolderGroupedIncludeTreeModel { get; set; } = new FolderIncludeTreeViewItem_Root(null, null);

        private IncludeGraph graph = null;
        private EnvDTE.Document currentDocument = null;

        public enum RefreshMode
        {
            ReportTime,
        }

        public static readonly string[] RefreshModeNames = new string[] { "Compile -d1reportTime" };

        public RefreshMode ActiveRefreshMode
        {
            get => activeRefreshMode;
            set
            {
                if (activeRefreshMode != value)
                {
                    activeRefreshMode = value;
                    OnNotifyPropertyChanged();
                    UpdateCanRefresh();
                }
            }
        }
        RefreshMode activeRefreshMode = RefreshMode.ReportTime;

        public IEnumerable<RefreshMode> PossibleRefreshModes => Enum.GetValues(typeof(RefreshMode)).Cast<RefreshMode>();

        public bool CanRefresh
        {
            get => canRefresh;
            private set { canRefresh = value; OnNotifyPropertyChanged(); }
        }
        private bool canRefresh = false;

        public string RefreshTooltip
        {
            get => refreshTooltip;
            set { refreshTooltip = value; OnNotifyPropertyChanged(); }
        }
        private string refreshTooltip = "";

        public bool RefreshInProgress
        {
            get => refreshInProgress;
            private set
            {
                refreshInProgress = value;
                UpdateCanRefresh();
                OnNotifyPropertyChanged();
                OnNotifyPropertyChanged(nameof(CanSave));
            }
        }
        private bool refreshInProgress = false;

        public string GraphRootFilename
        {
            get => graphRootFilename;
            private set { graphRootFilename = value; OnNotifyPropertyChanged(); }
        }
        private string graphRootFilename = "<No File>";

        public int NumIncludes
        {
            get => (graph?.GraphItems.Count ?? 1) - 1;
        }

        public bool CanSave
        {
            get => !refreshInProgress && graph != null && graph.GraphItems.Count > 0;
        }

        // Need to keep these guys alive.
        private EnvDTE.WindowEvents windowEvents;

        public IncludeGraphViewModel()
        {
            // UI update on dte events.
            var dte = VSUtils.GetDTE();
            if (dte != null)
            {
                windowEvents = dte.Events.WindowEvents;
                windowEvents.WindowActivated += (x, y) => UpdateActiveDoc();
            }

            UpdateActiveDoc();
        }

        private void UpdateActiveDoc()
        {
            var dte = VSUtils.GetDTE();
            var newDoc = dte?.ActiveDocument;
            if (newDoc != currentDocument)
            {
                currentDocument = newDoc;
                UpdateCanRefresh();
            }
        }

        private void UpdateCanRefresh()
        {
            // In any case we need a it to be a document.
            // Limiting to C++ document is a bit harsh though for the general case as we might not have this information depending on the project type.
            // This is why we just check for "having a document" here for now.
            if (currentDocument == null || RefreshInProgress)
            {
                CanRefresh = false;
                RefreshTooltip = "No open document";
            }
            else
            {
                if (activeRefreshMode == RefreshMode.ReportTime)
                {
                    CanRefresh = CompilationBasedGraphParser.CanPerformShowIncludeCompilation(currentDocument, out string reasonForFailure);
                    RefreshTooltip = reasonForFailure;
                }
                else
                {
                    CanRefresh = true;
                    RefreshTooltip = null;
                }
            }
        }

        public void RefreshIncludeGraph()
        {
            UpdateActiveDoc();

            var newGraph = new IncludeGraph();

            RefreshTooltip = "Update in Progress";
            GraphRootFilename = currentDocument?.Name ?? "<No File>";
            RefreshInProgress = true;

            try
            {
                switch (activeRefreshMode)
                {
                    case RefreshMode.ReportTime:
                        if (newGraph.AddIncludesRecursively_ShowIncludesCompilation(currentDocument, OnNewTreeComputed))
                        {
                            ResetIncludeTreeModel(null);
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
            catch(Exception e)
            {
                Output.Instance.WriteLine("Unexpected error when refreshing BuildTime Graph: {0}", e);
                OnNewTreeComputed(newGraph, false);
            }
        }

        public void SaveGraph(string filename)
        {
            DGMLGraph dgmlGraph = graph.ToDGMLGraph();
            dgmlGraph.Serialize(filename);
        }

        private void ResetIncludeTreeModel(IncludeGraph.GraphItem root)
        {
            HierarchyIncludeTreeModel.Reset(new IncludeGraph.Include() { IncludedFile = root }, "<root>");
            OnNotifyPropertyChanged(nameof(HierarchyIncludeTreeModel));

            FolderGroupedIncludeTreeModel.Reset(graph?.GraphItems, root);
            OnNotifyPropertyChanged(nameof(FolderGroupedIncludeTreeModel));

            OnNotifyPropertyChanged(nameof(CanSave));
        }

        private void OnNewTreeComputed(IncludeGraph graph, bool success)
        {
            RefreshInProgress = false;

            if (success)
            {
                this.graph = graph;

                var includeDirectories = VSUtils.GetProjectIncludeDirectories(currentDocument.ProjectItem.ContainingProject);
                includeDirectories.Insert(0, PathUtil.Normalize(currentDocument.Path) + Path.DirectorySeparatorChar);

                foreach (var item in graph.GraphItems)
                    item.FormattedName = IncludeFormatter.FormatPath(item.AbsoluteFilename, FormatterOptionsPage.PathMode.Shortest_AvoidUpSteps, includeDirectories);

                ResetIncludeTreeModel(graph.CreateOrGetItem(currentDocument.FullName, 0.0, out _));
            }

            OnNotifyPropertyChanged(nameof(NumIncludes));
        }
    }
}
