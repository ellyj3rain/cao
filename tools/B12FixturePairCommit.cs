using System;
using System.IO;

namespace ColonistAwareness.Tools
{
    public static class B12FixturePairCommit
    {
        public static void Commit(string active, string mirror,
            string content, Action afterFirstReplace = null)
        {
            string token = Guid.NewGuid().ToString("N");
            string activeTemp = Temporary(active, token);
            string mirrorTemp = Temporary(mirror, token);
            string activeBackup = activeTemp + ".bak";
            string mirrorBackup = mirrorTemp + ".bak";
            bool activeExisted = File.Exists(active);
            bool mirrorExisted = File.Exists(mirror);
            bool activeReplaced = false;
            bool mirrorReplaced = false;
            try
            {
                File.WriteAllText(activeTemp, content,
                    new System.Text.UTF8Encoding(false));
                File.WriteAllText(mirrorTemp, content,
                    new System.Text.UTF8Encoding(false));
                _ = System.Xml.Linq.XDocument.Load(activeTemp);
                _ = System.Xml.Linq.XDocument.Load(mirrorTemp);
                if (activeExisted) File.Copy(active, activeBackup, true);
                if (mirrorExisted) File.Copy(mirror, mirrorBackup, true);
                File.Move(activeTemp, active, true);
                activeReplaced = true;
                afterFirstReplace?.Invoke();
                File.Move(mirrorTemp, mirror, true);
                mirrorReplaced = true;
            }
            catch
            {
                Restore(active, activeBackup, activeExisted, activeReplaced);
                Restore(mirror, mirrorBackup, mirrorExisted, mirrorReplaced);
                throw;
            }
            finally
            {
                DeleteIfPresent(activeTemp);
                DeleteIfPresent(mirrorTemp);
                DeleteIfPresent(activeBackup);
                DeleteIfPresent(mirrorBackup);
            }
        }

        private static string Temporary(string target, string token)
        {
            return Path.Combine(Path.GetDirectoryName(target), "."
                + Path.GetFileName(target) + ".b12-" + token + ".tmp");
        }

        private static void Restore(string target, string backup,
            bool existed, bool replaced)
        {
            if (!replaced) return;
            if (existed && File.Exists(backup))
                File.Copy(backup, target, true);
            else if (!existed && File.Exists(target)) File.Delete(target);
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
