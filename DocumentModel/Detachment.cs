using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DocumentModel
{
    public class Detachment
    {
        static Detachment instance;
        Dictionary<string, string> adjException = new Dictionary<string,string>();
        Dictionary<string, string> nounException = new Dictionary<string, string>();
        Dictionary<string, string> verbException = new Dictionary<string, string>();

        HashSet<string> adjBaseForm = new HashSet<string>();
        HashSet<string> nounBaseForm = new HashSet<string>();
        HashSet<string> verbBaseForm = new HashSet<string>();

        List<DetachmentRule> adjRules = new List<DetachmentRule>();
        List<DetachmentRule> nounRules = new List<DetachmentRule>();
        List<DetachmentRule> verbRules = new List<DetachmentRule>();

        public static Detachment Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Detachment();
                }
                return instance;
            }
        }

        private Detachment()
        {
            LoadExceptionRules("adj.exc", adjException, adjBaseForm);
            LoadExceptionRules("noun.exc", nounException, nounBaseForm);
            LoadExceptionRules("verb.exc", verbException, verbBaseForm);

            adjRules.Add(new DetachmentRule("er", ""));
            adjRules.Add(new DetachmentRule("est", ""));

            nounRules.Add(new DetachmentRule("shes", "sh"));
            nounRules.Add(new DetachmentRule("ches", "ch"));
            nounRules.Add(new DetachmentRule("ies", "y"));
            nounRules.Add(new DetachmentRule("men", "man"));
            nounRules.Add(new DetachmentRule("zes", "z"));
            nounRules.Add(new DetachmentRule("xes", "x"));
            nounRules.Add(new DetachmentRule("ses", "s"));
            nounRules.Add(new DetachmentRule("as", "as")); // added
            nounRules.Add(new DetachmentRule("is", "is")); // added             
            nounRules.Add(new DetachmentRule("us", "us")); // added
            nounRules.Add(new DetachmentRule("ss", "ss")); // added
            nounRules.Add(new DetachmentRule("s", ""));

            verbRules.Add(new DetachmentRule("ies", "y"));
            verbRules.Add(new DetachmentRule("ied", "y")); // added
            verbRules.Add(new DetachmentRule("ing", "e"));
            verbRules.Add(new DetachmentRule("ing", ""));
            verbRules.Add(new DetachmentRule("es", "e"));
            verbRules.Add(new DetachmentRule("es", ""));
            verbRules.Add(new DetachmentRule("ed", "e"));
            verbRules.Add(new DetachmentRule("ed", ""));
        }

        private void LoadExceptionRules(string src, Dictionary<string, string> exception, HashSet<string> baseForm)
        {
            string line;
            StreamReader reader = new StreamReader(new FileStream(src, FileMode.Open));
            while ((line = reader.ReadLine()) != null)
            {
                string[] ss = line.Split(' ');
                baseForm.Add(ss[ss.Length - 1]);
                for (int i = 0; i < ss.Length - 1; i++)
                {
                    if (!exception.ContainsKey(ss[i]))
                    {
                        exception.Add(ss[i], ss[ss.Length - 1]);
                    }
                }
            }
            reader.Close();
        }

        public string Detach(string w)
        {
            string word = w.ToLower();
            string detached;
            // by exception
            if ((detached = DetachByException(word, nounException, nounBaseForm))!=null)
            {
                return detached;
            }
            if ((detached = DetachByException(word, verbException, verbBaseForm)) != null)
            {
                return detached;
            }
            if ((detached = DetachByException(word, adjException, adjBaseForm)) != null)
            {
                return detached;
            }            

            // by suffix
            if ((detached = DetachBySuffix(word, nounRules)) != null)
            {
                return detached;
            }

            if ((detached = DetachBySuffix(word, verbRules)) != null)
            {
                return detached;
            }

            if ((detached = DetachBySuffix(word, adjRules)) != null)
            {
                return detached;
            }
            
            return word;
        }

        private string DetachByException(string word, Dictionary<string, string> exception, HashSet<string> baseForm)
        {
            string detached;
            if (baseForm.Contains(word))
            {
                return word;
            }
            if (exception.TryGetValue(word, out detached))
            {
                return detached;
            }
            return null;
        }

        private string DetachBySuffix(string word, List<DetachmentRule> rules)
        {
            string detached;
            for (int i = 0; i < rules.Count; i++)
            {
                if (word.EndsWith(rules[i].Suffix))
                {
                    detached = word.Substring(0, word.Length - rules[i].Suffix.Length) + rules[i].Ending;
                    return detached;
                }
            }
            return null;
        }

        private class DetachmentRule
        {
            public string Suffix
            {
                get;
                set;
            }

            public string Ending
            {
                get;
                set;
            }

            public DetachmentRule(string o, string r)
            {
                Suffix = o;
                Ending = r;
            }
        }

        static void Main(string[] args)
        {
            string v=Detachment.Instance.Detach("modified");
        }
    }    
}
