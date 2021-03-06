﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentModel;
using System.IO;

namespace PrecedenceModel
{
    delegate ICollection<int> ListProperties(InstanceDB db, int idx);
    delegate List<PrecedenceProperty> BuildProperties(List<int> orig);

    public class PrecedenceModel
    {
        public PrecedenceModel(DocModelDictionary dictionary, DocModelDictionary clsDictionary)
        {
            this.dictionary = dictionary;
            this.clsDictionary = clsDictionary;
        }

        void LoadInstances(int limit)
        {
            if (dbs == null)
                dbs = new List<InstanceDB>();
            else
                dbs.Clear();

            int[] listOfTopicNumbers = { 500 };
            for (int i = 0; i < listOfTopicNumbers.Length; i++)
            {
                InstanceDB.collectionName = "ldadocs_" + listOfTopicNumbers[i]+ "_all";
                InstanceDB db = new InstanceDB();
                db.LoadFromDBByDataSet("doc_set_cls_1000", limit);
                db.LoadLDAModel("model\\ldamodel_" + listOfTopicNumbers[i] + ".beta", listOfTopicNumbers[i], dictionary);
                dbs.Add(db);
            }
        }

        public void DiscoverPrecedence()
        {
            LoadInstances(50000);
            precedenceRelations = new List<PrecedenceRelation>();
            for (int i = 0; i < dbs.Count; i++)
            {
                string precedenceName = "precedence\\precedence_" + dbs[i].Vocabulary.Count;
                List<KeyValuePair<PrecedenceRelation, int>> precedences =
                    DiscoverPrecedence(dbs[i]
                        , PrecedenceProperty.Convert
                        , (db, idx) => { LDAModel ldaDoc = (LDAModel)db[idx]; return ldaDoc.ClassLabels; }
                        , (db, idx) => { return ((LDAModel)db[idx]).WordList; }
                        );
                SavePrecendence(precedenceName, precedences, dbs[i].Vocabulary);

                precedenceName = "precedence\\precedence_" + dbs[i].Vocabulary.Count + "_2";
                precedences =
                    DiscoverPrecedence(dbs[i]
                        , PrecedenceProperty.Convert2
                        , (db, idx) => { LDAModel ldaDoc = (LDAModel)db[idx]; return ldaDoc.ClassLabels; }
                        , (db, idx) => { return ((LDAModel)db[idx]).WordList; }
                        );
                SavePrecendence(precedenceName, precedences, dbs[i].Vocabulary);                
            }

            for (int i = 0; i < dbs.Count; i++)
            {
                for (int j = i + 1; j < dbs.Count; j++)
                {
                    string precedenceName = "precedence\\precedence_" + dbs[j].Vocabulary.Count + "_" + dbs[i].Vocabulary.Count;
                    List<KeyValuePair<PrecedenceRelation, int>> precedences = DiscoverPrecedence(dbs[i]
                        , PrecedenceProperty.Convert
                        , (db, idx) =>
                        {
                            string docId = db[idx].DocID;
                            int thisIdx = dbs[j].SearchByDocID(docId);
                            if (thisIdx >= 0)
                                return ((LDAModel)dbs[j][thisIdx]).WordList;
                            else
                                return new List<int>();
                        }
                        , (db, idx) => { return ((LDAModel)db[idx]).WordList; }
                        );
                    SavePrecendence(precedenceName, precedences, dbs[j].Vocabulary, dbs[i].Vocabulary);

                    precedenceName = "precedence\\precedence_" + dbs[j].Vocabulary.Count + "_" + dbs[i].Vocabulary.Count+ "_2";
                    precedences = DiscoverPrecedence(dbs[i]
                        , PrecedenceProperty.Convert2
                        , (db, idx) =>
                        {
                            string docId = db[idx].DocID;
                            int thisIdx = dbs[j].SearchByDocID(docId);
                            if (thisIdx >= 0)
                                return ((LDAModel)dbs[j][thisIdx]).WordList;
                            else
                                return new List<int>();
                        }
                        , (db, idx) => { return ((LDAModel)db[idx]).WordList; }
                        );
                    SavePrecendence(precedenceName, precedences, dbs[j].Vocabulary, dbs[i].Vocabulary);
                }
            }

            dbs.Clear();
        }

        List<KeyValuePair<PrecedenceRelation, int>> DiscoverPrecedence(InstanceDB db, BuildProperties buildP, ListProperties listP1, ListProperties listP2)
        {            
            Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>> good = new Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>>();
            for (int i = 0; i < db.Count; i++)
            {
                LDAModel ldaDoc = (LDAModel)db[i];
                HashSet<PrecedenceProperty> p1List = PrecedenceProperty.Convert(listP1(db, i));
                List<PrecedenceProperty> p2List = buildP((List<int>)listP2(db, i));

                // clear bad
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
                            }
                            removed.Clear();
                        }
                    }
                    removed = null;
                }

                // add new
                for (int j1 = 0; j1 < p2List.Count; j1++)
                {
                    PrecedenceProperty p2 = p2List[j1];
                    Dictionary<PrecedenceRelation, int> pps = null;
                    if (good.TryGetValue(p2, out pps)) // there exist some instances having p2 may or may not not have p1
                    {
                        foreach (PrecedenceProperty p1 in p1List)
                        {
                            PrecedenceRelation pp = new PrecedenceRelation(p1, p2);
                            int count = 0;
                            if (pps.TryGetValue(pp, out count))
                            {
                                pps[pp] = count + 1;
                            }                            
                        }                        
                    }
                    else // p2 not exists
                    {
                        pps = new Dictionary<PrecedenceRelation, int>();
                        foreach (PrecedenceProperty p1 in p1List)
                        {
                            PrecedenceRelation pp = new PrecedenceRelation(p1, p2);
                            pps.Add(pp, 1);                            
                        }
                        good.Add(p2, pps);
                    }   
                }                
            }
            return ToPrecedences(good);
        }

        //public List<KeyValuePair<PrecedenceRelation, int>> DiscoverPrecedence(InstanceDB db1, InstanceDB db2)
        //{
        //    HashSet<PrecedenceRelation> bad = new HashSet<PrecedenceRelation>();
        //    Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>> good = new Dictionary<PrecedenceProperty, Dictionary<PrecedenceRelation, int>>();
        //    for (int i = 0; i < db1.Count; i++)
        //    {
        //        List<PrecedenceProperty> p1List = PrecedenceProperty.Convert(((LDAModel)db1[i]).WordList);
        //        List<PrecedenceProperty> p2List = PrecedenceProperty.Convert(((LDAModel)db2[i]).WordList);

        //        // add to bad
        //        if (good.Count > 0)
        //        {
        //            List<PrecedenceRelation> removed = new List<PrecedenceRelation>();
        //            for (int j1 = 0; j1 < p2List.Count; j1++)
        //            {
        //                Dictionary<PrecedenceRelation, int> pps = null;
        //                PrecedenceProperty p2 = p2List[j1];
        //                if (good.TryGetValue(p2, out pps))
        //                {
        //                    foreach (KeyValuePair<PrecedenceRelation, int> kvp in pps)
        //                    {
        //                        PrecedenceProperty p1 = kvp.Key.P1;
        //                        if (!p1List.Contains(p1))
        //                        {
        //                            removed.Add(kvp.Key);
        //                        }
        //                    }
        //                    for (int l = 0; l < removed.Count; l++)
        //                    {
        //                        pps.Remove(removed[l]);
        //                        bad.Add(removed[l]);
        //                    }
        //                    removed.Clear();
        //                }
        //            }
        //            removed = null;
        //        }

        //        for (int k = 0; k < p1List.Count; k++)
        //        {
        //            PrecedenceProperty p1 = p1List[k];
        //            for (int j1 = 0; j1 < p2List.Count; j1++)
        //            {
        //                PrecedenceProperty p2 = p2List[j1];
        //                PrecedenceRelation pp = new PrecedenceRelation(p1, p2);
        //                if (!bad.Contains(pp))
        //                {
        //                    Dictionary<PrecedenceRelation, int> pps = null;
        //                    if (good.TryGetValue(p2, out pps))
        //                    {
        //                        int count = 0;
        //                        if (pps.TryGetValue(pp, out count))
        //                        {
        //                            pps[pp] = count + 1;
        //                        }
        //                        else // there exist some instances having p2 but does not having p1
        //                        {
        //                            bad.Add(pp);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        pps = new Dictionary<PrecedenceRelation, int>();
        //                        pps.Add(pp, 1);
        //                        good.Add(p2, pps);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    return ToPrecedences(good);
        //}

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
            if (precedences.Count > 0)
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
                        sb.Append(p.Value + "       ");
                        sb.Append(PrecedenceProperty.ConvertWords(p1, clsDictionary));
                        sb.Append(" ------> ");
                        sb.Append(PrecedenceProperty.ConvertWords(p2, vocabulary));

                        writer.WriteLine(sb.ToString());
                        precedenceRelations.Add(p.Key);
                    }
                }
                writer.Close();
            }
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
                    sb.Append(p.Value + "       ");
                    sb.Append(PrecedenceProperty.ConvertWords(p1, vocabulary1));
                    sb.Append(" ------> ");
                    sb.Append(PrecedenceProperty.ConvertWords(p2, vocabulary2));

                    writer.WriteLine(sb.ToString());
                }
            }
            writer.Close();
        }

        public List<PrecedenceRelation> PrecedenceRelations
        {
            get
            {
                return precedenceRelations;
            }
        }        

        List<InstanceDB> dbs;
        DocModelDictionary dictionary;
        DocModelDictionary clsDictionary;
        List<PrecedenceRelation> precedenceRelations;
        int support = 3;
    }
}
