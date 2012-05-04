using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;

namespace PrecedenceModel
{
    class PrecedenceRelation
    {
        PrecedenceProperty p1;
        PrecedenceProperty p2;

        public PrecedenceRelation(PrecedenceProperty p1, PrecedenceProperty p2)
        {
            this.p1 = p1;           
            this.p2 = p2;            
        }

        public override bool Equals(object obj)
        {
            if (obj is PrecedenceRelation)
            {
                PrecedenceRelation p = (PrecedenceRelation)obj;
                return p.p1.Equals(p1) && p.p2.Equals(p2);                
            }
            return false;
        }

        public override int GetHashCode()
        {
            int h = p1.GetHashCode() + p2.GetHashCode();
            return h;
        }

        public PrecedenceProperty P1
        {
            get { return p1; }
        }

        public PrecedenceProperty P2
        {
            get { return p2; }
        }
    }

    class PrecedenceProperty
    {
        int[] p;

        public PrecedenceProperty(int[] p)
        {
            this.p = p;
            Array.Sort(this.p);            
        }

        public PrecedenceProperty(int p)
        {
            this.p = new int[] { p };
        }

        public override bool Equals(object obj)
        {
            if (obj is PrecedenceProperty)
            {
                PrecedenceProperty prop = (PrecedenceProperty)obj;
                if (p.Length == prop.p.Length)
                {
                    for (int i = 0; i < p.Length; i++)
                        if (p[i] != prop.p[i]) return false;
                    return true;
                }                
            }
            return false;
        }        

        public override int GetHashCode()
        {
            int h = 0;
            for (int i = 0; i < p.Length; i++)
                h += p[i].GetHashCode();    
            return h;
        }

        public static HashSet<PrecedenceProperty> Convert(ICollection<int> orig)
        {
            HashSet<PrecedenceProperty> converted = new HashSet<PrecedenceProperty>();
            foreach (int key in orig)
            {
                converted.Add(new PrecedenceProperty(key));
            }
            return converted;
        }

        public static List<PrecedenceProperty> Convert(List<int> orig)
        {
            List<PrecedenceProperty> converted = new List<PrecedenceProperty>();
            foreach (int key in orig)
            {
                converted.Add(new PrecedenceProperty(key));
            }
            return converted;
        }

        public static List<PrecedenceProperty> Convert2(List<int> orig)
        {
            List<PrecedenceProperty> converted = new List<PrecedenceProperty>();
            for(int i=0; i<orig.Count; i++)
            {
                for (int j = i + 1; j < orig.Count; j++)
                {
                    int[] p = new int[] { orig[i], orig[j] };
                    converted.Add(new PrecedenceProperty(p));
                }
            }
            return converted;
        }

        public static List<PrecedenceProperty> Convert3(List<int> orig)
        {
            List<PrecedenceProperty> converted = new List<PrecedenceProperty>();
            for (int i = 0; i < orig.Count; i++)
            {
                for (int j = i + 1; j < orig.Count; j++)
                {
                    for (int k = j + 1; k < orig.Count; k++)
                    {
                        int[] p = new int[] { orig[i], orig[j], orig[k] };
                        converted.Add(new PrecedenceProperty(p));
                    }
                }
            }
            return converted;
        }

        public static string ConvertWords(PrecedenceProperty p, DocModelDictionary dictionary)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            for (int i = 0; i < p.p.Length; i++)
            {
                int wordKey = p.p[i];
                sb.Append(" [");
                sb.Append(dictionary.GetKey(wordKey));                
                sb.Append("],");
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(" }");
            return sb.ToString();
        }

        public static string ConvertWords(PrecedenceProperty p, Dictionary<int, string[]> vocabulary)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            for (int i = 0; i < p.p.Length; i++)
            {
                int wordKey = p.p[i];
                sb.Append(" [");
                for (int j = 0; j < vocabulary[wordKey].Length; j++)
                {
                    sb.Append(vocabulary[wordKey][j]);
                    sb.Append(":");
                }
                sb.Remove(sb.Length - 1, 1);
                sb.Append("],");
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(" }");
            return sb.ToString();
        }

    }
}
