using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;
using System.IO;

namespace PrecedenceModel
{
    class PrecedenceModel
    {
        public PrecedenceModel(DocModelDictionary dictionary, DocModelDictionary clsDictionary)
        {
            this.dictionary = dictionary;
            this.clsDictionary = clsDictionary;
            relations = new HashSet<int[]>();
        }

        public void LoadInstances()
        {
            if (dbs == null)
                dbs = new List<InstanceDB>();
            else
                dbs.Clear();

            int[] listOfTopicNumbers = { 20, 10 };
            for (int i = 0; i < listOfTopicNumbers.Length; i++)
            {
                InstanceDB.collectionName = "ldadocs_" + listOfTopicNumbers[i];
                InstanceDB db = new InstanceDB();
                db.LoadInstances();
                db.LoadLDAModel("model\\ldamodel_" + listOfTopicNumbers[i] + ".beta", listOfTopicNumbers[i], dictionary);
                dbs.Add(db);
            }
        }

        public void DiscoverPrecedence()
        {
            for (int i = 0; i < dbs.Count; i++)
            {
                string precedenceName = "precedence\\precedence_" + dbs[i].Vocabulary.Count;
                List<KeyValuePair<PrecedenceRelation, int>> precedences = DiscoverPrecedence(dbs[i]);
                SavePrecendence(precedenceName, precedences, dbs[i].Vocabulary);
            }

            for (int i = 0; i < dbs.Count; i++)
            {
                for (int j = i + 1; j < dbs.Count; j++)
                {
                    string precedenceName = "precedence\\precedence_" + dbs[j].Vocabulary.Count + "_" + dbs[i].Vocabulary.Count;
                    List<KeyValuePair<PrecedenceRelation, int>> precedences = DiscoverPrecedence(dbs[j], dbs[i]);
                    SavePrecendence(precedenceName, precedences, dbs[j].Vocabulary, dbs[i].Vocabulary);
                }
            }
        }

        public List<KeyValuePair<PrecedenceRelation, int>> DiscoverPrecedence(InstanceDB db)
        {
            HashSet<PrecedenceRelation> bad = new HashSet<PrecedenceRelation>();
            Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>> good = new Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>>();
            for (int i = 0; i < db.Count; i++)
            {
                LDAModel ldaDoc = (LDAModel)db[i];
                HashSet<PrecedenceProperty> p1List = PrecedenceProperty.Convert(ldaDoc.ClassLabels);
                List<PrecedenceProperty> p2List = PrecedenceProperty.Convert(((LDAModel)db[i]).WordList);

                // add to bad
                if (good.Count > 0)
                {
                    List<PrecedenceRelation> removed = new List<PrecedenceRelation>();
                    for (int j1 = 0; j1 < p2List.Count; j1++)
                    {
                        Dictionary<PrecedenceRelation, int> pps = null;
                        PrecedenceProperty p2 = p2List[j1];
                        if (good.TryGetValue(p2, out pps))
                        {
                            foreach (KeyValuePair<PrecedenceRelation, int> kvp in pps)
                            {
                                PrecedenceProperty p1 = kvp.Key.P1;
                                if (!p1List.Contains(p1))
                                {
                                    removed.Add(kvp.Key);
                                }
                            }
                            for (int l = 0; l < removed.Count; l++)
                            {
                                pps.Remove(removed[l]);
                                bad.Add(removed[l]);
                            }
                            removed.Clear();
                        }
                    }
                    removed = null;
                }

                foreach (PrecedenceProperty p1 in p1List)
                {
                    for (int j1 = 0; j1 < p2List.Count; j1++)
                    {
                        PrecedenceProperty p2 = p2List[j1];
                        PrecedenceRelation pp = new PrecedenceRelation(p1, p2);
                        if (!bad.Contains(pp))
                        {
                            Dictionary<PrecedenceRelation, int> pps = null;
                            if (good.TryGetValue(p2, out pps))
                            {
                                int count = 0;
                                if (pps.TryGetValue(pp, out count))
                                {
                                    pps[pp] = count + 1;
                                }
                                else
                                {
                                    pps.Add(pp, 1);
                                }
                            }
                            else
                            {
                                pps = new Dictionary<PrecedenceRelation, int>();
                                pps.Add(pp, 1);
                                good.Add(p2, pps);
                            }
                        }
                    }
                }
            }
            return ToPrecedences(good);
        }

        public List<KeyValuePair<PrecedenceRelation, int>> DiscoverPrecedence(InstanceDB db1, InstanceDB db2)
        {
            HashSet<PrecedenceRelation> bad = new HashSet<PrecedenceRelation>();
            Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>> good = new Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>>();
            for (int i = 0; i < db1.Count; i++)
            {
                List<PrecedenceProperty> p1List = PrecedenceProperty.Convert(((LDAModel)db1[i]).WordList);
                List<PrecedenceProperty> p2List = PrecedenceProperty.Convert(((LDAModel)db2[i]).WordList);

                // add to bad
                if (good.Count > 0)
                {
                    List<PrecedenceRelation> removed = new List<PrecedenceRelation>();
                    for (int j1 = 0; j1 < p2List.Count; j1++)
                    {
                        Dictionary<PrecedenceRelation, int> pps = null;
                        PrecedenceProperty p2 = p2List[j1];
                        if (good.TryGetValue(p2, out pps))
                        {
                            foreach (KeyValuePair<PrecedenceRelation, int> kvp in pps)
                            {
                                PrecedenceProperty p1 = kvp.Key.P1;
                                if (!p1List.Contains(p1))
                                {
                                    removed.Add(kvp.Key);
                                }
                            }
                            for (int l = 0; l < removed.Count; l++)
                            {
                                pps.Remove(removed[l]);
                                bad.Add(removed[l]);
                            }
                            removed.Clear();
                        }
                    }
                    removed = null;
                }

                for (int k = 0; k < p1List.Count; k++)
                {
                    PrecedenceProperty p1 = p1List[k];
                    for (int j1 = 0; j1 < p2List.Count; j1++)
                    {
                        PrecedenceProperty p2 = p2List[j1];
                        PrecedenceRelation pp = new PrecedenceRelation(p1, p2);
                        if (!bad.Contains(pp))
                        {
                            Dictionary<PrecedenceRelation, int> pps = null;
                            if (good.TryGetValue(p2, out pps))
                            {
                                int count = 0;
                                if (pps.TryGetValue(pp, out count))
                                {
                                    pps[pp] = count + 1;
                                }
                                else
                                {
                                    pps.Add(pp, 1);
                                }
                            }
                            else
                            {
                                pps = new Dictionary<PrecedenceRelation, int>();
                                pps.Add(pp, 1);
                                good.Add(p2, pps);
                            }
                        }
                    }
                }
            }
            return ToPrecedences(good);
        }

        private List<KeyValuePair<PrecedenceRelation, int>> ToPrecedences(Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>> good)
        {
            List<KeyValuePair<PrecedenceRelation, int>> precedences = new List<KeyValuePair<PrecedenceRelation, int>>();
            foreach (KeyValuePair<PrecedenceProperty, Dictionary<PrecedenceRelation, int>> kvp in good)
            {
                foreach (KeyValuePair<PrecedenceRelation, int> p in kvp.Value)
                {
                    precedences.Add(p);
                }
            }
            return precedences;
        }

        public void SavePrecendence(string precedenceName, List<KeyValuePair<PrecedenceRelation, int>> precedences, Dictionary<int, string[]> vocabulary)
        {
            StreamWriter writer = new StreamWriter(new FileStream(precedenceName, FileMode.Create));
            for (int i = 0; i < precedences.Count; i++)
            {
                KeyValuePair<PrecedenceRelation, int> p = precedences[i];
                if (p.Value >= support)
                {
                    PrecedenceProperty p1 = p.Key.P1;
                    PrecedenceProperty p2 = p.Key.P2;
                    StringBuilder sb = new StringBuilder();
                    sb.Append(PrecedenceProperty.ConvertWords(p1, clsDictionary));
                    sb.Append(" ------> ");
                    sb.Append(PrecedenceProperty.ConvertWords(p2, vocabulary));
                                        
                    writer.WriteLine(sb.ToString());
                }
            }
            writer.Close();
        }

        public void SavePrecendence(string precedenceName, List<KeyValuePair<PrecedenceRelation, int>> precedences, Dictionary<int, string[]> vocabulary1, Dictionary<int, string[]> vocabulary2)
        {
            StreamWriter writer = new StreamWriter(new FileStream(precedenceName, FileMode.Create));
            for (int i = 0; i < precedences.Count; i++)
            {
                KeyValuePair<PrecedenceRelation, int> p = precedences[i];
                if (p.Value >= support)
                {
                    PrecedenceProperty p1 = p.Key.P1;
                    PrecedenceProperty p2 = p.Key.P2;
                    StringBuilder sb = new StringBuilder();
                    sb.Append(PrecedenceProperty.ConvertWords(p1, vocabulary1));     
                    sb.Append(" ------> ");
                    sb.Append(PrecedenceProperty.ConvertWords(p2, vocabulary2));
                    
                    writer.WriteLine(sb.ToString());
                }
            }
            writer.Close();
        }

        HashSet<int[]> relations;
        List<InstanceDB> dbs;
        DocModelDictionary dictionary;
        DocModelDictionary clsDictionary;
        int support = 1;

    }
}
