using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace PoECV
{
    public class ConversationFile
    {
        public readonly string Path;

        public ConversationFile(string path)
        {
            Path = path;
        }

        public override string ToString()
        {
            return Path.Substring(88, Path.Length - 88 - 13);
        }
    }

    //Base class for any class with properties that need to notify the view about updates.
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    //Translate companion names to and from the GUID in the conversation files.
    public static class GuidLookup
    {
        public static readonly Dictionary<string, string> Lookup = new Dictionary<string, string>();

        static GuidLookup()
        {
            var list = new Dictionary<string, string>
            {
                {"Edér", "b1a7e800-0000-0000-0000-000000000000"},
                {"Durance", "b1a7e801-0000-0000-0000-000000000000"},
                {"Aloth", "b1a7e803-0000-0000-0000-000000000000"},
                {"Kana Rua", "b1a7e804-0000-0000-0000-000000000000"},
                {"Sagani", "b1a7e805-0000-0000-0000-000000000000"},
                {"Pallegina", "b1a7e806-0000-0000-0000-000000000000"},
                {"Grieving Mother", "b1a7e807-0000-0000-0000-000000000000"},
                {"Hiravias", "b1a7e808-0000-0000-0000-000000000000"},
                {"Calisca", "b1a7e809-0000-0000-0000-000000000000"},
                {"Heodan", "b1a7e810-0000-0000-0000-000000000000"}
            };
            foreach (var entry in list)
            {
                Lookup[entry.Key] = entry.Value;
                Lookup[entry.Value] = entry.Key;
            }
        }
    }
}