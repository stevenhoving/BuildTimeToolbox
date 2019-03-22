using System.Collections.Generic;

namespace IncludeToolbox.GraphWindow
{
    public abstract class IncludeTreeViewItem : PropertyChangedBase
    {
        public string Name { get; protected set; }

        public string AbsoluteFilename { get; protected set; }

        public double Time { get; protected set; }

        public int Count { get; protected set; }

        /// <summary>
        /// List of children, builds tree lazily.
        /// </summary>
        public abstract IReadOnlyList<IncludeTreeViewItem> Children { get; }

        static protected IReadOnlyList<IncludeTreeViewItem> emptyList = new IncludeTreeViewItem[0];

        public IncludeTreeViewItem()
        {
        }

        protected void NotifyAllPropertiesChanged()
        {
            OnNotifyPropertyChanged(nameof(Name));
            OnNotifyPropertyChanged(nameof(Time));
            OnNotifyPropertyChanged(nameof(Children));
            OnNotifyPropertyChanged(nameof(AbsoluteFilename));

            //var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, element, index);
            //OnCollectionChanged(nameof(Children));
        }

        /// <summary>
        /// Navigates to to the file/include in the IDE.
        /// </summary>
        abstract public void NavigateToInclude();
    }
}
