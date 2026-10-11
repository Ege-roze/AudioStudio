using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace ses_uyg
{
    internal static class SoundLibraryStore
    {
        internal static List<SoundItem> Load(string path)
        {
            if (!File.Exists(path)) return new List<SoundItem>();
            using (var reader = new StreamReader(path))
            {
                var sounds = (List<SoundItem>)new XmlSerializer(typeof(List<SoundItem>)).Deserialize(reader);
                if (sounds == null || sounds.Any(sound => sound == null || string.IsNullOrWhiteSpace(sound.Name) || string.IsNullOrWhiteSpace(sound.Path)))
                    throw UiText.Error("LibraryInvalid");
                return sounds;
            }
        }

        internal static void Save(string path, List<SoundItem> sounds)
        {
            string target = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(target), ".soundboard-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(List<SoundItem>)).Serialize(stream, sounds);
                    stream.Flush(true);
                }
                // A failed write/replace leaves the previous library intact.
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
