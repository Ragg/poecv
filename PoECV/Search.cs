using System.Collections.Generic;
using AlphanumComparator;
using GalaSoft.MvvmLight.CommandWpf;

namespace PoECV
{
    public class ParameterSelection : ObservableObject
    {
        private string _selection;

        public ParameterSelection()
        {
            Parameters = new SortedSet<string>(new AlphanumComparator<string>());
            ClearCommand = new RelayCommand(Clear);
        }

        public RelayCommand ClearCommand { get; private set; }
        public SortedSet<string> Parameters { get; private set; }

        public string Selection
        {
            get { return _selection; }
            set
            {
                if (_selection != value)
                {
                    _selection = value;
                    OnPropertyChanged();
                }
            }
        }

        private void Clear()
        {
            Selection = null;
        }
    }

    public class SearchResult
    {
        public SearchResult(ConversationFile file, int id)
        {
            File = file;
            Id = id;
        }

        public ConversationFile File { get; private set; }
        public int Id { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} in {1}", Id, File);
        }
    }
}